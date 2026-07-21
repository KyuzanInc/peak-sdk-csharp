#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
canonicalizer="$repo_root/tools/package/canonicalize-nuget-package.py"
project="$repo_root/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj"

for tool in dotnet python3; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "required tool is unavailable: $tool" >&2
    exit 1
  fi
done

if [[ ! -f "$canonicalizer" ]]; then
  echo "NuGet package canonicalizer is missing: $canonicalizer" >&2
  exit 1
fi

fixture=$(mktemp -d)
trap 'rm -rf "$fixture"' EXIT
first="$fixture/first"
second="$fixture/second"
mkdir -p "$first" "$second"

TurnkeySourceProject= dotnet restore "$project" \
  --force-evaluate \
  --configfile "$repo_root/nuget.public-ci.config" \
  -p:NuGetLockFilePath="$fixture/peak-pack.packages.lock.json"
TurnkeySourceProject= dotnet pack "$project" \
  -c Release --no-build --no-restore --output "$first"
TurnkeySourceProject= dotnet pack "$project" \
  -c Release --no-build --no-restore --output "$second"

for package in \
  "$first/KyuzanInc.Peak.Sdk.1.0.0.nupkg" \
  "$first/KyuzanInc.Peak.Sdk.1.0.0.snupkg" \
  "$second/KyuzanInc.Peak.Sdk.1.0.0.nupkg" \
  "$second/KyuzanInc.Peak.Sdk.1.0.0.snupkg"; do
  python3 "$canonicalizer" "$package"
done

cmp --silent \
  "$first/KyuzanInc.Peak.Sdk.1.0.0.nupkg" \
  "$second/KyuzanInc.Peak.Sdk.1.0.0.nupkg"
cmp --silent \
  "$first/KyuzanInc.Peak.Sdk.1.0.0.snupkg" \
  "$second/KyuzanInc.Peak.Sdk.1.0.0.snupkg"

cp "$first/KyuzanInc.Peak.Sdk.1.0.0.nupkg" "$fixture/canonical.nupkg"
python3 "$canonicalizer" "$fixture/canonical.nupkg"
cmp --silent "$first/KyuzanInc.Peak.Sdk.1.0.0.nupkg" "$fixture/canonical.nupkg"

echo "NuGet package canonicalization regression passed"
