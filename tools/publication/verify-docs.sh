#!/usr/bin/env bash
set -euo pipefail

repo_root=${PUBLICATION_REPO_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}
required_files=(
  README.md
  SECURITY.md
  CONTRIBUTING.md
  CHANGELOG.md
  docs/release-process.md
  docs/adr/README.md
  docs/adr/0001-source-provenance-and-compatibility.md
  docs/adr/0002-public-api-contract-and-generated-client.md
  docs/adr/0003-public-source-private-package-distribution.md
  docs/compatibility/upstream-pins.md
  docs/compatibility/coverage-map.md
)
stale_files=(
  README.md
  packages/peak-sdk-csharp/README.public.md
  docs/development.md
  docs/architecture.md
  docs/sync-rules.md
)
stale_patterns='pre-release|0\.1\.0-alpha|publishes to nuget\.org|upstream-snapshots/peak-sdk-unity/'
failed=0

if ! git -C "$repo_root" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  printf 'PUBLICATION_REPO_ROOT is not a Git worktree: %s\n' "$repo_root" >&2
  exit 1
fi

for path in "${required_files[@]}"; do
  if [[ ! -f "$repo_root/$path" ]]; then
    printf 'missing required public document: %s\n' "$path" >&2
    failed=1
  elif ! git -C "$repo_root" ls-files --error-unmatch "$path" >/dev/null 2>&1; then
    printf 'required public document is not tracked: %s\n' "$path" >&2
    failed=1
  fi
done

for path in "${stale_files[@]}"; do
  if [[ ! -f "$repo_root/$path" ]]; then
    printf 'missing publication-copy document: %s\n' "$path" >&2
    failed=1
    continue
  fi

  while IFS= read -r line; do
    printf '%s:%s\n' "$path" "$line" >&2
    failed=1
  done < <(LC_ALL=C grep -a -nE -- "$stale_patterns" "$repo_root/$path" | cut -d: -f1 || true)
done

if (( failed )); then
  exit 1
fi

echo 'documentation verification passed'
