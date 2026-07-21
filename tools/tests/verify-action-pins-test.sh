#!/usr/bin/env bash
set -euo pipefail

tools_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
verifier="$tools_dir/verify-action-pins.sh"
fixture=$(mktemp -d)
trap 'rm -rf "$fixture"' EXIT

mkdir -p "$fixture/.github/workflows"
git -C "$fixture" init -q
git -C "$fixture" config user.email fixture@example.com
git -C "$fixture" config user.name Fixture
git -C "$fixture" config commit.gpgSign false

checkout_sha=9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0
dotnet_sha=c2fa09f4bde5ebb9d1777cf28262a3eb3db3ced7

cat > "$fixture/.github/pin-actions.txt" <<MANIFEST
actions/checkout
  pinned_sha:     $checkout_sha
  pinned_tag:     v7.0.0
  used_in:        verify.yml

actions/setup-dotnet
  pinned_sha:     $dotnet_sha
  pinned_tag:     v5.2.0
  used_in:        second.yml
MANIFEST

cat > "$fixture/.github/workflows/verify.yml" <<WORKFLOW
name: Verify
jobs:
  verify:
    steps:
      - uses: actions/checkout@$checkout_sha
WORKFLOW

cat > "$fixture/.github/workflows/second.yml" <<WORKFLOW
name: Second
jobs:
  verify:
    steps:
      - 'uses': actions/setup-dotnet@$dotnet_sha
WORKFLOW

git -C "$fixture" add .
git -C "$fixture" commit -qm fixture

run_expect_fail() {
  if ACTION_PIN_REPO_ROOT="$fixture" \
    ACTION_PIN_VERIFY_TARGETS="${1-}" \
    "$verifier" >/dev/null 2>&1; then
    echo "expected Action-pin verifier to fail${2:+: $2}" >&2
    exit 1
  fi
}

ACTION_PIN_REPO_ROOT="$fixture" "$verifier" >/dev/null
ACTION_PIN_REPO_ROOT="$fixture" ACTION_PIN_VERIFY_TARGETS=verify.yml \
  "$verifier" >/dev/null

sed -i.bak "s/@$dotnet_sha/@v5/" "$fixture/.github/workflows/second.yml"
ACTION_PIN_REPO_ROOT="$fixture" ACTION_PIN_VERIFY_TARGETS=verify.yml \
  "$verifier" >/dev/null
run_expect_fail $'verify.yml\nsecond.yml' "newline-delimited target was ignored"
git -C "$fixture" restore .github/workflows/second.yml

unapproved_sha=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
sed -i.bak "s/@$checkout_sha/@$unapproved_sha/" "$fixture/.github/workflows/verify.yml"
run_expect_fail verify.yml "unapproved immutable SHA"
git -C "$fixture" restore .github/workflows/verify.yml

sed -i.bak "s/@$checkout_sha/@v7/" "$fixture/.github/workflows/verify.yml"
run_expect_fail verify.yml "mutable Action tag"
git -C "$fixture" restore .github/workflows/verify.yml

cat > "$fixture/.github/workflows/untracked.yml" <<WORKFLOW
name: Untracked
jobs:
  verify:
    steps:
      - uses: actions/checkout@$checkout_sha
WORKFLOW

for invalid_targets in \
  '/''tmp/verify.yml' \
  '../verify.yml' \
  'nested/verify.yml' \
  'verify.yaml' \
  'missing.yml' \
  'untracked.yml' \
  'verify.yml verify.yml'; do
  run_expect_fail "$invalid_targets" "invalid focused target: $invalid_targets"
done

cat > "$fixture/.github/workflows/escaped.yml" <<'WORKFLOW'
name: Escaped
jobs:
  verify:
    steps:
      - "\u0075ses": actions/upload-artifact@v7
WORKFLOW
git -C "$fixture" add .github/workflows/escaped.yml
run_expect_fail escaped.yml "escaped mutable uses key"

ACTION_PIN_REPO_ROOT="$fixture" \
  ACTION_PIN_VERIFY_TARGETS=$'verify.yml\nsecond.yml' \
  "$verifier" >/dev/null

echo "action pin verifier regression tests passed"
