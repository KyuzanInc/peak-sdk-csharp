#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
verifier="$repo_root/tools/publication/verify-docs.sh"

if [[ ! -f "$verifier" ]]; then
  echo "verify-docs verifier is missing: $verifier" >&2
  exit 1
fi

fixture=$(mktemp -d)
trap 'rm -rf "$fixture"' EXIT

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

run_expect_fail() {
  if PUBLICATION_REPO_ROOT="$fixture" bash "$verifier" >/dev/null 2>&1; then
    echo "expected document verifier to fail" >&2
    exit 1
  fi
}

for path in "${required_files[@]}" "${stale_files[@]}"; do
  mkdir -p "$fixture/$(dirname "$path")"
  printf 'public documentation\n' > "$fixture/$path"
done

git -C "$fixture" init -q
git -C "$fixture" config user.email fixture@example.com
git -C "$fixture" config user.name Fixture
git -C "$fixture" config commit.gpgSign false
git -C "$fixture" add .
git -C "$fixture" commit -qm fixture

git -C "$fixture" rm --cached -q SECURITY.md
run_expect_fail
git -C "$fixture" add SECURITY.md

for stale in \
  'pre-release' \
  '0.1.0-alpha' \
  'publishes to nuget.org' \
  'upstream-snapshots/peak-sdk-unity/'; do
  printf '%s\n' "$stale" > "$fixture/README.md"
  run_expect_fail
done

printf 'public documentation\n' > "$fixture/README.md"
PUBLICATION_REPO_ROOT="$fixture" bash "$verifier"
echo "verify-docs regression tests passed"
