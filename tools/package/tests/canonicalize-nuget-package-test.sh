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
  -c Release --no-build --no-restore -p:RepositoryBranch= --output "$first"
TurnkeySourceProject= dotnet pack "$project" \
  -c Release --no-build --no-restore -p:RepositoryBranch= --output "$second"

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

branch_package="$fixture/branch-metadata.nupkg"
python3 - "$first/KyuzanInc.Peak.Sdk.1.0.0.nupkg" "$branch_package" <<'PY'
import sys
import xml.etree.ElementTree as ET
import zipfile

source, destination = sys.argv[1:]
with zipfile.ZipFile(source, "r") as archive:
    entries = [(item, archive.read(item)) for item in archive.infolist()]

with zipfile.ZipFile(destination, "w") as archive:
    nuspec_seen = False
    for item, content in entries:
        if item.filename.endswith(".nuspec"):
            if nuspec_seen:
                raise SystemExit("fixture source unexpectedly contains duplicate nuspec entries")
            nuspec_seen = True
            root = ET.fromstring(content)
            repositories = [
                element for element in root.iter()
                if element.tag.rsplit("}", 1)[-1] == "repository"
            ]
            if len(repositories) != 1:
                raise SystemExit("fixture source must contain exactly one repository element")
            repositories[0].set("branch", "main")
            content = ET.tostring(root, encoding="utf-8", xml_declaration=True)
        archive.writestr(item, content)

if not nuspec_seen:
    raise SystemExit("fixture source does not contain a nuspec")
PY

python3 "$canonicalizer" "$branch_package"
python3 - "$branch_package" <<'PY'
import sys
import xml.etree.ElementTree as ET
import zipfile

with zipfile.ZipFile(sys.argv[1], "r") as archive:
    nuspec_names = [name for name in archive.namelist() if name.endswith(".nuspec")]
    if len(nuspec_names) != 1:
        raise SystemExit("canonical package must contain exactly one nuspec")
    root = ET.fromstring(archive.read(nuspec_names[0]))

repositories = [
    element for element in root.iter()
    if element.tag.rsplit("}", 1)[-1] == "repository"
]
if len(repositories) != 1 or "branch" in repositories[0].attrib:
    raise SystemExit("canonical package retained repository branch metadata")
PY

echo "NuGet package canonicalization regression passed"
