#!/usr/bin/env bash
set -euo pipefail

repo_root=${PUBLICATION_REPO_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}
forbidden_paths='^(CLAUDE\.md|plans/|docs/superpowers/|upstream-snapshots/peak-sdk-unity/|artifacts/|local-feed/)|\.(nupkg|snupkg)$'
secret_patterns='gh[pousr]_[A-Za-z0-9_]{20,}|-----BEGIN [A-Z ]*PRIVATE KEY-----|/''Users/[^/[:space:]]+/|[A-Za-z]:\\Users\\'
openapi_patterns='local''host|127\.0\.0\.1|0\.0\.0\.0|10\.[0-9]+\.[0-9]+\.[0-9]+|192\.168\.[0-9]+\.[0-9]+|172\.(1[6-9]|2[0-9]|3[01])\.[0-9]+\.[0-9]+|[A-Za-z0-9._%+-]+@kyuzan\.com|development\.yourapi\.com|production\.yourapi\.com|peak-dev\.xyz|c0731898-7908-472a-aaff-373417cdb213|9c7076e7-c8d4-4b7b-a484-e9ed28f3931d|eyJwdWJsaWNLZXki'
contract_path='upstream-snapshots/peak-server-openapi/public-api.yaml'
manifest_path='tests/UpstreamSources/peak-server-openapi-public-api.sha256'
failed=0

fail() {
  printf '%s\n' "$1" >&2
  failed=1
}

print_findings() {
  local path=$1
  local pattern=$2
  local line
  while IFS= read -r line; do
    printf '%s:%s\n' "$path" "$line" >&2
  done < <(LC_ALL=C grep -a -nE -- "$pattern" "$repo_root/$path" | cut -d: -f1 || true)
}

sha256() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{print tolower($1)}'
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$1" | awk '{print tolower($1)}'
  elif command -v openssl >/dev/null 2>&1; then
    openssl dgst -sha256 "$1" | awk '{print tolower($NF)}'
  else
    fail 'no SHA-256 command is available'
    return 1
  fi
}

if ! git -C "$repo_root" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  printf 'PUBLICATION_REPO_ROOT is not a Git worktree: %s\n' "$repo_root" >&2
  exit 1
fi

while IFS= read -r -d '' path; do
  if [[ $path =~ $forbidden_paths ]]; then
    fail "$path"
  fi
done < <(git -C "$repo_root" ls-files -z)

while IFS= read -r -d '' path; do
  case "$path" in
    tools/publication/verify-public-tree.sh|tools/publication/tests/verify-public-tree-test.sh)
      continue
      ;;
  esac

  if LC_ALL=C grep -a -qE -- "$secret_patterns" "$repo_root/$path"; then
    print_findings "$path" "$secret_patterns"
    failed=1
  fi

  if [[ $path == "$contract_path" || $path == packages/peak-public-api-client-csharp/* ]] &&
    LC_ALL=C grep -a -qE -- "$openapi_patterns" "$repo_root/$path"; then
    print_findings "$path" "$openapi_patterns"
    failed=1
  fi
done < <(git -C "$repo_root" ls-files -z)

if [[ ! -f "$repo_root/$manifest_path" ]]; then
  fail "missing OpenAPI checksum manifest: $manifest_path"
elif ! git -C "$repo_root" ls-files --error-unmatch "$manifest_path" >/dev/null 2>&1; then
  fail "OpenAPI checksum manifest is not tracked: $manifest_path"
elif [[ ! -f "$repo_root/$contract_path" ]]; then
  fail "missing OpenAPI contract: $contract_path"
elif ! git -C "$repo_root" ls-files --error-unmatch "$contract_path" >/dev/null 2>&1; then
  fail "OpenAPI contract is not tracked: $contract_path"
else
  manifest_line_count=$(awk 'NF {count++} END {print count + 0}' "$repo_root/$manifest_path")
  manifest_record=$(awk 'NF {print; exit}' "$repo_root/$manifest_path")
  read -r expected manifest_contract extra <<< "$manifest_record" || true
  if [[ $manifest_line_count != 1 || ! $expected =~ ^[a-f0-9]{64}$ || $manifest_contract != "$contract_path" || -n ${extra:-} ]]; then
    fail "invalid OpenAPI checksum manifest: $manifest_path"
  else
    actual=$(sha256 "$repo_root/$contract_path") || true
    if [[ ${actual:-} != "$expected" ]]; then
      fail "OpenAPI checksum mismatch: $contract_path"
    fi
  fi
fi

if (( failed )); then
  exit 1
fi

echo 'public-tree verification passed'
