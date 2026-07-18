#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
bootstrap="$repo_root/tools/compatibility/prepare-turnkey-local-feed.sh"
commit=5c219364d678c3a72786a2a89fbc7bbd4931be5e
url="https://github.com/KyuzanInc/turnkey-sdk-csharp/archive/${commit}.tar.gz"

fixture=$(mktemp -d)
trap 'rm -rf "$fixture"' EXIT

archive="$fixture/turnkey-sdk-csharp-${commit}.tar.gz"
curl --fail --location --silent --show-error "$url" --output "$archive"

if [[ ! -f "$bootstrap" ]]; then
  echo "Turnkey local-feed bootstrap is missing: $bootstrap" >&2
  exit 1
fi

feed="$fixture/feed"
source_tree="$fixture/source"
TURNKEY_SOURCE_ARCHIVE="$archive" bash "$bootstrap" "$feed" "$source_tree"

package_count=$(find "$feed" -maxdepth 1 -type f -name '*.nupkg' -print | wc -l | tr -d '[:space:]')
if [[ "$package_count" != 1 ]]; then
  echo "expected exactly one Turnkey package, found $package_count" >&2
  exit 1
fi

expected_package="$feed/KyuzanInc.Turnkey.Sdk.1.0.0.nupkg"
actual_package=$(find "$feed" -maxdepth 1 -type f -name '*.nupkg' -print)
if [[ "$actual_package" != "$expected_package" ]]; then
  echo "unexpected package name: $actual_package" >&2
  exit 1
fi

if ! unzip -p "$expected_package" '*.nuspec' | grep -Fq '<version>1.0.0</version>'; then
  echo "Turnkey package metadata does not contain version 1.0.0" >&2
  exit 1
fi

source_project="$source_tree/src/turnkey-sdk-csharp.csproj"
if [[ ! -f "$source_project" ]]; then
  echo "Turnkey source project was not prepared: $source_project" >&2
  exit 1
fi
if [[ -e "$source_tree/.git" ]]; then
  echo "prepared Turnkey source must not contain Git metadata" >&2
  exit 1
fi

tampered="$fixture/tampered.tar.gz"
cp "$archive" "$tampered"
printf '\000' | dd of="$tampered" bs=1 seek=0 conv=notrunc 2>/dev/null

if TURNKEY_SOURCE_ARCHIVE="$tampered" \
  bash "$bootstrap" "$fixture/tampered-feed" "$fixture/tampered-source" >/dev/null 2>&1; then
  echo "checksum mismatch was accepted" >&2
  exit 1
fi

sentinel="$fixture/external-sentinel"
printf 'external sentinel must remain unchanged\n' > "$sentinel"
symlink_feed="$fixture/symlink-feed"
mkdir -p "$symlink_feed"
ln -s "$sentinel" "$symlink_feed/KyuzanInc.Turnkey.Sdk.1.0.0.nupkg"

symlink_source="$fixture/symlink-source"
if TURNKEY_SOURCE_ARCHIVE="$archive" \
  bash "$bootstrap" "$symlink_feed" "$symlink_source" >/dev/null 2>&1; then
  echo "symlink package target was accepted" >&2
  exit 1
fi

if [[ $(cat "$sentinel") != 'external sentinel must remain unchanged' ]]; then
  echo "symlink package target overwrote the external sentinel" >&2
  exit 1
fi
if [[ -e "$symlink_source" || -L "$symlink_source" ]]; then
  echo "failed package preparation left a source output behind" >&2
  exit 1
fi

unexpected_sentinel="$fixture/unexpected-external-sentinel"
printf 'unexpected external sentinel must remain unchanged\n' > "$unexpected_sentinel"
unexpected_symlink_feed="$fixture/unexpected-symlink-feed"
mkdir -p "$unexpected_symlink_feed"
ln -s "$unexpected_sentinel" "$unexpected_symlink_feed/Unexpected.Package.1.0.0.nupkg"

if TURNKEY_SOURCE_ARCHIVE="$archive" \
  bash "$bootstrap" "$unexpected_symlink_feed" "$fixture/unexpected-source" >/dev/null 2>&1; then
  echo "unexpected package symlink was accepted" >&2
  exit 1
fi

if [[ $(cat "$unexpected_sentinel") != 'unexpected external sentinel must remain unchanged' ]]; then
  echo "unexpected package symlink changed the external sentinel" >&2
  exit 1
fi

existing_source="$fixture/existing-source"
mkdir -p "$existing_source"
printf 'existing source sentinel must remain unchanged\n' > "$existing_source/sentinel"
if TURNKEY_SOURCE_ARCHIVE="$archive" \
  bash "$bootstrap" "$fixture/existing-source-feed" "$existing_source" >/dev/null 2>&1; then
  echo "existing source output was accepted" >&2
  exit 1
fi
if [[ $(cat "$existing_source/sentinel") != 'existing source sentinel must remain unchanged' ]]; then
  echo "existing source output was modified" >&2
  exit 1
fi

echo "prepare-turnkey-local-feed regression tests passed"
