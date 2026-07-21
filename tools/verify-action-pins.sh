#!/usr/bin/env bash
set -euo pipefail

repo_root=${ACTION_PIN_REPO_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}
manifest="$repo_root/.github/pin-actions.txt"
observed=$(mktemp)
expected=$(mktemp)
selected=$(mktemp)
trap 'rm -f "$observed" "$expected" "$selected"' EXIT
uses_pattern="(\"uses\"|'uses'|uses)[[:space:]]*:[[:space:]]*([^[:space:]#]+)"

validate_mapping_key_safety() {
  local workflow=$1
  awk -v workflow="$(basename "$workflow")" '
    function reject(message) {
      print "::error::" workflow " " message "." > "/dev/stderr"
      exit 1
    }
    BEGIN { single_quote = sprintf("%c", 39) }
    {
      candidate=$0
      sub(/^[ \t]+/, "", candidate)
      sub(/^-[ \t]+/, "", candidate)
      if (candidate == "" || substr(candidate, 1, 1) == "#") next

      if (substr(candidate, 1, 1) == "\"") {
        escaped=0
        closing=0
        for (position=2; position<=length(candidate); position++) {
          character=substr(candidate, position, 1)
          if (character == "\\") {
            escaped=1
            position++
            continue
          }
          if (character == "\"") {
            closing=position
            break
          }
        }
        if (!closing) reject("has an invalid quoted mapping key")
        remainder=substr(candidate, closing + 1)
        sub(/^[ \t]+/, "", remainder)
        if (substr(remainder, 1, 1) == ":" && escaped) {
          reject("has an escaped double-quoted mapping key")
        }
        next
      }

      if (substr(candidate, 1, 1) == single_quote) {
        closing=0
        for (position=2; position<=length(candidate); position++) {
          if (substr(candidate, position, 1) != single_quote) continue
          if (position < length(candidate) && substr(candidate, position + 1, 1) == single_quote) {
            position++
            continue
          }
          closing=position
          break
        }
        if (!closing) reject("has an invalid quoted mapping key")
        next
      }

      if (candidate ~ /^\? / || (candidate ~ /^[*&!{\[]/ && index(candidate, ":") > 0)) {
        reject("has an unsupported complex mapping key")
      }
    }
  ' "$workflow"
}

if ! git -C "$repo_root" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  printf 'ACTION_PIN_REPO_ROOT is not a Git worktree: %s\n' "$repo_root" >&2
  exit 1
fi
if [[ ! -f "$manifest" ]] ||
  ! git -C "$repo_root" ls-files --error-unmatch .github/pin-actions.txt >/dev/null 2>&1; then
  echo "::error::.github/pin-actions.txt must exist and be tracked."
  exit 1
fi

workflows=()
focused=0
seen_targets=$'\n'
if [[ -n ${ACTION_PIN_VERIFY_TARGETS:-} ]]; then
  focused=1
  while IFS= read -r target; do
    if [[ "$target" == *..* || "$target" == */* || "$target" == *\\* ||
          ! "$target" =~ ^[A-Za-z0-9][A-Za-z0-9._-]*\.ya?ml$ ]]; then
      printf 'invalid Action-pin workflow target: %s\n' "$target" >&2
      exit 1
    fi
    if [[ "$seen_targets" == *$'\n'"$target"$'\n'* ]]; then
      printf 'duplicate Action-pin workflow target: %s\n' "$target" >&2
      exit 1
    fi

    workflow_path=".github/workflows/$target"
    if [[ ! -f "$repo_root/$workflow_path" ||
          ! -r "$repo_root/$workflow_path" ]]; then
      printf 'Action-pin workflow target does not exist: %s\n' "$target" >&2
      exit 1
    fi
    if ! git -C "$repo_root" ls-files --error-unmatch "$workflow_path" >/dev/null 2>&1; then
      printf 'Action-pin workflow target is not tracked: %s\n' "$target" >&2
      exit 1
    fi

    workflows+=("$repo_root/$workflow_path")
    printf '%s\n' "$target" >> "$selected"
    seen_targets+="$target"$'\n'
  done < <(
    printf '%s\n' "$ACTION_PIN_VERIFY_TARGETS" |
      awk '{ for (field = 1; field <= NF; field++) print $field }'
  )

  (( ${#workflows[@]} > 0 )) || {
    echo 'ACTION_PIN_VERIFY_TARGETS did not contain a workflow filename' >&2
    exit 1
  }
else
  while IFS= read -r -d '' workflow_path; do
    target=${workflow_path##*/}
    [[ "$workflow_path" == ".github/workflows/$target" ]] || continue
    workflows+=("$repo_root/$workflow_path")
    printf '%s\n' "$target" >> "$selected"
  done < <(
    git -C "$repo_root" ls-files -z -- \
      '.github/workflows/*.yml' '.github/workflows/*.yaml'
  )

  (( ${#workflows[@]} > 0 )) || {
    echo "::error::No committed workflow YAML files were found."
    exit 1
  }
  for workflow in "${workflows[@]}"; do
    [[ -f "$workflow" ]] || {
      printf 'committed workflow is missing from the worktree: %s\n' \
        "$(basename "$workflow")" >&2
      exit 1
    }
  done
fi

for workflow in "${workflows[@]}"; do
  validate_mapping_key_safety "$workflow"
  while IFS= read -r line; do
    [[ "$line" =~ $uses_pattern ]] || continue
    spec=${BASH_REMATCH[2]}
    [[ "$spec" == ./* ]] && continue
    [[ "$spec" =~ ^([^@]+)@([0-9a-f]{40})$ ]] || {
      echo "::error::$(basename "$workflow") has a mutable or invalid Action reference."
      exit 1
    }
    action_ref=${BASH_REMATCH[1]}
    owner=${action_ref%%/*}
    remainder=${action_ref#*/}
    repository=${remainder%%/*}
    printf '%s/%s %s %s\n' "$owner" "$repository" "${BASH_REMATCH[2]}" \
      "$(basename "$workflow")" >> "$observed"
  done < "$workflow"
done

if (( focused )); then
  awk '
    NR == FNR { selected[$1] = 1; next }
    /^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/ {
      action=$0; sha=""; used=0; next
    }
    /^  pinned_sha:[[:space:]]+/ { sha=$2; next }
    /^  used_in:[[:space:]]+/ {
      used=1
      if ($2 in selected) print action, sha, $2
      next
    }
    used && /^  [A-Za-z_]+:/ { used=0; next }
    used && /^[[:space:]]+[^[:space:]]/ {
      if ($1 in selected) print action, sha, $1
    }
  ' "$selected" "$manifest" > "$expected"
else
  awk '
    /^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/ {
      action=$0; sha=""; used=0; next
    }
    /^  pinned_sha:[[:space:]]+/ { sha=$2; next }
    /^  used_in:[[:space:]]+/ {
      used=1; print action, sha, $2; next
    }
    used && /^  [A-Za-z_]+:/ { used=0; next }
    used && /^[[:space:]]+[^[:space:]]/ {
      print action, sha, $1
    }
  ' "$manifest" > "$expected"
fi

LC_ALL=C sort -u -o "$observed" "$observed"
LC_ALL=C sort -u -o "$expected" "$expected"
diff -u "$expected" "$observed" || {
  echo "::error::Workflow Action pins and pin-actions.txt differ."
  exit 1
}
echo "action pin verification passed"
