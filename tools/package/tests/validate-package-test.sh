#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
validator="$repo_root/tools/package/validate-package.sh"
canonicalizer="$repo_root/tools/package/canonicalize-nuget-package.py"
project="$repo_root/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj"

for tool in dotnet python3; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "required tool is unavailable: $tool" >&2
    exit 1
  fi
done

if [[ ! -f "$validator" ]]; then
  echo "package validator is missing: $validator" >&2
  exit 1
fi
if [[ ! -f "$canonicalizer" ]]; then
  echo "package canonicalizer is missing: $canonicalizer" >&2
  exit 1
fi

fixture=$(mktemp -d)
verifier_directory="$repo_root/tools/package/PackageSymbolVerifier"
saved_verifier_outputs="$fixture/saved-verifier-outputs"
mkdir -p "$saved_verifier_outputs"

cleanup() {
  for output_name in bin obj; do
    if [[ -e "$verifier_directory/$output_name" ]]; then
      rm -rf "$verifier_directory/$output_name"
    fi
    if [[ -e "$saved_verifier_outputs/$output_name" ]]; then
      mv "$saved_verifier_outputs/$output_name" "$verifier_directory/$output_name"
    fi
  done
  rm -rf "$fixture"
}
trap cleanup EXIT

for output_name in bin obj; do
  if [[ -e "$verifier_directory/$output_name" ]]; then
    mv "$verifier_directory/$output_name" "$saved_verifier_outputs/$output_name"
  fi
done
feed="$fixture/feed"
mkdir -p "$feed"

TurnkeySourceProject= dotnet restore "$project" \
  --force-evaluate \
  --configfile "$repo_root/nuget.public-ci.config" \
  -p:NuGetLockFilePath="$fixture/peak-pack.packages.lock.json"
TurnkeySourceProject= dotnet pack \
  "$project" -c Release --no-build --no-restore -p:RepositoryBranch= \
  --output "$feed"

package="$feed/KyuzanInc.Peak.Sdk.1.0.0.nupkg"
symbols="$feed/KyuzanInc.Peak.Sdk.1.0.0.snupkg"
if [[ ! -f "$package" || ! -f "$symbols" ]]; then
  echo "real package or symbol package was not produced" >&2
  exit 1
fi

python3 "$canonicalizer" "$package"
python3 "$canonicalizer" "$symbols"
bash "$validator" "$package" "$symbols"

rewrite_package() {
  local source=$1
  local destination=$2
  local mode=$3

  python3 - "$source" "$destination" "$mode" <<'PY'
import re
import sys
import warnings
import zipfile

source, destination, mode = sys.argv[1:]
warnings.filterwarnings("ignore", message="Duplicate name:")

with zipfile.ZipFile(source, "r") as archive:
    entries = [(item, archive.read(item)) for item in archive.infolist()]
    content_by_name = {item.filename: content for item, content in entries}

with zipfile.ZipFile(destination, "w") as archive:
    nuspec_seen = False
    for item, content in entries:
        name = item.filename
        if mode == "missing-license" and name == "LICENSE":
            continue
        if mode == "missing-symbol" and name == "lib/net8.0/KyuzanInc.Peak.Sdk.pdb":
            continue
        if mode == "nonportable-symbol" and name == "lib/net8.0/KyuzanInc.Peak.Sdk.pdb":
            content = b"not-a-portable-pdb"
        if mode == "truncated-symbol" and name == "lib/net8.0/KyuzanInc.Peak.Sdk.pdb":
            content = b"BSJB"
        if mode == "cross-tfm-symbol" and name == "lib/net8.0/KyuzanInc.Peak.Sdk.pdb":
            content = content_by_name["lib/netstandard2.1/KyuzanInc.Peak.Sdk.pdb"]
        if name.endswith(".nuspec"):
            if nuspec_seen:
                raise SystemExit("fixture source unexpectedly contains duplicate nuspec entries")
            nuspec_seen = True
            if mode == "wrong-dependency":
                old = b'<dependency id="KyuzanInc.Turnkey.Sdk" version="[1.0.0]"'
                new = b'<dependency id="KyuzanInc.Turnkey.Sdk" version="[0.1.0-alpha.0]"'
                if content.count(old) != 3:
                    raise SystemExit("fixture source does not have three exact Turnkey dependencies")
                content = content.replace(old, new)
            elif mode == "duplicate-version":
                marker = b"<version>1.0.0</version>"
                if content.count(marker) != 1:
                    raise SystemExit("fixture source does not have one package version")
                content = content.replace(marker, marker + marker)
            elif mode == "nested-version":
                marker = b"<version>1.0.0</version>"
                replacement = b"<version>1.0.0<version>0.1.0-alpha.0</version></version>"
                if content.count(marker) != 1:
                    raise SystemExit("fixture source does not have one package version")
                content = content.replace(marker, replacement)
            elif mode == "nested-turnkey-dependency":
                marker = (
                    b'<dependency id="KyuzanInc.Turnkey.Sdk" version="[1.0.0]" '
                    b'exclude="Build,Analyzers" />'
                )
                replacement = (
                    b'<dependency id="KyuzanInc.Turnkey.Sdk" version="[1.0.0]" '
                    b'exclude="Build,Analyzers">'
                    b'<dependency id="KyuzanInc.Turnkey.Sdk" '
                    b'version="[0.1.0-alpha.0]" />'
                    b'</dependency>'
                )
                if content.count(marker) != 3:
                    raise SystemExit("fixture source does not have three exact Turnkey dependencies")
                content = content.replace(marker, replacement, 1)
            elif mode == "missing-group-dependency":
                pattern = re.compile(
                    rb'(<group targetFramework="net8\.0">.*?)'
                    rb'\s*<dependency id="KyuzanInc\.Turnkey\.Sdk" version="\[1\.0\.0\]"[^>]*/>',
                    re.DOTALL,
                )
                content, count = pattern.subn(rb"\1", content, count=1)
                if count != 1:
                    raise SystemExit("fixture source does not have the net8.0 Turnkey dependency")
            elif mode == "malformed-nuspec":
                content = b"<package><metadata>"
        archive.writestr(item, content)

    if mode == "duplicate-license":
        archive.writestr("LICENSE", b"duplicate\n")
    elif mode == "unsafe-path":
        archive.writestr("../escape", b"unsafe\n")
    elif mode == "drive-relative-path":
        archive.writestr("C:escape", b"unsafe\n")
    elif mode == "license-file-directory-collision":
        archive.writestr("LICENSE/", b"")
    elif mode == "extra-net9-library":
        archive.writestr(
            "lib/net9.0/KyuzanInc.Peak.Sdk.dll",
            content_by_name["lib/net8.0/KyuzanInc.Peak.Sdk.dll"],
        )
PY
}

accepted_bypasses=0

expect_rejected() {
  local description=$1
  local bad_package=$2
  local bad_symbols=$3
  local output

  if output=$(bash "$validator" "$bad_package" "$bad_symbols" 2>&1); then
    echo "validator accepted corrupted fixture: $description" >&2
    accepted_bypasses=$((accepted_bypasses + 1))
    return
  fi
  if [[ -z "$output" ]]; then
    echo "validator rejected without a diagnostic: $description" >&2
    accepted_bypasses=$((accepted_bypasses + 1))
  fi
}

rewrite_package "$package" "$fixture/missing-license.nupkg" missing-license
expect_rejected "missing LICENSE" "$fixture/missing-license.nupkg" "$symbols"

rewrite_package "$package" "$fixture/wrong-dependency.nupkg" wrong-dependency
expect_rejected "wrong Turnkey dependency" "$fixture/wrong-dependency.nupkg" "$symbols"

rewrite_package "$package" "$fixture/duplicate-license.nupkg" duplicate-license
expect_rejected "duplicate ZIP path" "$fixture/duplicate-license.nupkg" "$symbols"

rewrite_package "$package" "$fixture/unsafe-path.nupkg" unsafe-path
expect_rejected "unsafe ZIP path" "$fixture/unsafe-path.nupkg" "$symbols"

rewrite_package "$package" "$fixture/duplicate-version.nupkg" duplicate-version
expect_rejected "duplicate nuspec version" "$fixture/duplicate-version.nupkg" "$symbols"

rewrite_package "$package" "$fixture/nested-version.nupkg" nested-version
expect_rejected "nested scalar package version" "$fixture/nested-version.nupkg" "$symbols"

rewrite_package "$package" "$fixture/nested-turnkey-dependency.nupkg" nested-turnkey-dependency
expect_rejected "nested conflicting Turnkey dependency" "$fixture/nested-turnkey-dependency.nupkg" "$symbols"

rewrite_package "$package" "$fixture/missing-group-dependency.nupkg" missing-group-dependency
expect_rejected "missing dependency from a target group" "$fixture/missing-group-dependency.nupkg" "$symbols"

rewrite_package "$package" "$fixture/malformed-nuspec.nupkg" malformed-nuspec
expect_rejected "malformed nuspec" "$fixture/malformed-nuspec.nupkg" "$symbols"

rewrite_package "$symbols" "$fixture/missing-symbol.snupkg" missing-symbol
expect_rejected "missing portable PDB" "$package" "$fixture/missing-symbol.snupkg"

rewrite_package "$symbols" "$fixture/nonportable-symbol.snupkg" nonportable-symbol
expect_rejected "non-portable PDB" "$package" "$fixture/nonportable-symbol.snupkg"

rewrite_package "$symbols" "$fixture/truncated-symbol.snupkg" truncated-symbol
expect_rejected "truncated portable-PDB header" "$package" "$fixture/truncated-symbol.snupkg"

rewrite_package "$symbols" "$fixture/cross-tfm-symbol.snupkg" cross-tfm-symbol
expect_rejected "cross-TFM PDB substitution" "$package" "$fixture/cross-tfm-symbol.snupkg"

rewrite_package "$package" "$fixture/extra-net9-library.nupkg" extra-net9-library
expect_rejected "unsupported net9.0 SDK library" "$fixture/extra-net9-library.nupkg" "$symbols"

rewrite_package "$package" "$fixture/drive-relative-path.nupkg" drive-relative-path
expect_rejected "Windows drive-relative ZIP path" "$fixture/drive-relative-path.nupkg" "$symbols"

rewrite_package "$package" "$fixture/license-file-directory-collision.nupkg" license-file-directory-collision
expect_rejected "LICENSE file-directory collision" "$fixture/license-file-directory-collision.nupkg" "$symbols"

if [[ -e "$verifier_directory/bin" || -e "$verifier_directory/obj" ]]; then
  echo "package validator left PackageSymbolVerifier bin/obj in the repository" >&2
  accepted_bypasses=$((accepted_bypasses + 1))
fi

if (( accepted_bypasses )); then
  echo "package validator accepted $accepted_bypasses corrupt fixture(s)" >&2
  exit 1
fi

printf 'package validator regression passed\n'
