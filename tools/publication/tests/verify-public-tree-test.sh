#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
verifier="$repo_root/tools/publication/verify-public-tree.sh"

if [[ ! -f "$verifier" ]]; then
  echo "verify-public-tree verifier is missing: $verifier" >&2
  exit 1
fi

fixture=$(mktemp -d)
trap 'rm -rf "$fixture"' EXIT

sha256() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{print tolower($1)}'
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$1" | awk '{print tolower($1)}'
  elif command -v openssl >/dev/null 2>&1; then
    openssl dgst -sha256 "$1" | awk '{print tolower($NF)}'
  else
    echo "no SHA-256 command is available" >&2
    return 1
  fi
}

run_expect_fail() {
  if PUBLICATION_REPO_ROOT="$fixture" bash "$verifier" >/dev/null 2>&1; then
    echo "expected public-tree verifier to fail" >&2
    exit 1
  fi
}

write_clean_contract() {
  mkdir -p "$fixture/upstream-snapshots/peak-server-openapi" "$fixture/tests/UpstreamSources"
  printf 'openapi: 3.0.0\ninfo:\n  title: Public API\n' > "$fixture/upstream-snapshots/peak-server-openapi/public-api.yaml"
  cp "$fixture/upstream-snapshots/peak-server-openapi/public-api.yaml" "$fixture/clean-public-api.yaml"
  write_manifest
}

write_manifest() {
  printf '%s  %s\n' "$(sha256 "$fixture/upstream-snapshots/peak-server-openapi/public-api.yaml")" \
    'upstream-snapshots/peak-server-openapi/public-api.yaml' > "$fixture/tests/UpstreamSources/peak-server-openapi-public-api.sha256"
}

git -C "$fixture" init -q
git -C "$fixture" config user.email fixture@example.com
git -C "$fixture" config user.name Fixture
git -C "$fixture" config commit.gpgSign false
write_clean_contract
git -C "$fixture" add .
git -C "$fixture" commit -qm fixture

for path in \
  'CLAUDE.md' \
  'plans/internal.md' \
  'docs/superpowers/plans/internal.md' \
  'upstream-snapshots/peak-sdk-unity/Runtime/PeakSdk.cs' \
  'artifacts/KyuzanInc.Peak.Sdk.1.0.0.nupkg'; do
  mkdir -p "$fixture/$(dirname "$path")"
  printf 'fixture\n' > "$fixture/$path"
  git -C "$fixture" add "$path"
  run_expect_fail
  git -C "$fixture" reset -q HEAD -- "$path"
  rm -f "$fixture/$path"
done

for content in \
  'ghp_''123456789012345678901234567890123456' \
  '/''Users/example/private/file'; do
  printf '%s\n' "$content" > "$fixture/source.txt"
  git -C "$fixture" add source.txt
  run_expect_fail
  git -C "$fixture" reset -q HEAD -- source.txt
  rm -f "$fixture/source.txt"
done

contract="$fixture/upstream-snapshots/peak-server-openapi/public-api.yaml"
manifest="$fixture/tests/UpstreamSources/peak-server-openapi-public-api.sha256"

printf 'unexpected second manifest record\n' >> "$manifest"
git -C "$fixture" add "$manifest"
run_expect_fail
write_manifest
git -C "$fixture" add "$manifest"

git -C "$fixture" rm --cached -q "$contract"
run_expect_fail
git -C "$fixture" add "$contract"

git -C "$fixture" rm --cached -q "$manifest"
run_expect_fail
git -C "$fixture" add "$manifest"

for content in \
  'http://127.''0.0.1:3000' \
  'developer@''kyuzan.com' \
  'c0731898-7908-472a-aaff-373417cdb213'; do
  cp "$fixture/clean-public-api.yaml" "$contract"
  printf '%s\n' "$content" >> "$contract"
  write_manifest
  git -C "$fixture" add "$contract"
  git -C "$fixture" add tests/UpstreamSources/peak-server-openapi-public-api.sha256
  run_expect_fail
  cp "$fixture/clean-public-api.yaml" "$contract"
  write_manifest
  git -C "$fixture" add "$contract"
  git -C "$fixture" add tests/UpstreamSources/peak-server-openapi-public-api.sha256
done

printf 'changed: true\n' >> "$contract"
git -C "$fixture" add "$contract"
run_expect_fail
cp "$fixture/clean-public-api.yaml" "$contract"
git -C "$fixture" add "$contract"

PUBLICATION_REPO_ROOT="$fixture" bash "$verifier"
echo "verify-public-tree regression tests passed"
