#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "usage: $0 <nupkg> <snupkg>" >&2
  exit 1
fi

for tool in dotnet python3; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "required tool is unavailable: $tool" >&2
    exit 1
  fi
done

nupkg=$1
snupkg=$2
repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
verifier_project="$repo_root/tools/package/PackageSymbolVerifier/PackageSymbolVerifier.csproj"
for package in "$nupkg" "$snupkg"; do
  if [[ ! -f "$package" || -L "$package" ]]; then
    echo "package is not a regular file: $package" >&2
    exit 1
  fi
done

python3 - "$nupkg" "$snupkg" <<'PY'
import pathlib
import posixpath
import re
import stat
import sys
import unicodedata
import zipfile
import xml.etree.ElementTree as ET

NUPKG = pathlib.Path(sys.argv[1])
SNUPKG = pathlib.Path(sys.argv[2])
ASSEMBLY = "KyuzanInc.Peak.Sdk"
TURNKEY = "KyuzanInc.Turnkey.Sdk"
EXPECTED_VERSION = "1.0.0"
EXPECTED_TURNKEY_RANGE = "[1.0.0]"
EXPECTED_REPOSITORY = "https://github.com/KyuzanInc/peak-sdk-csharp"


class ValidationError(Exception):
    pass


def fail(message):
    raise ValidationError(message)


def local_name(tag):
    return tag.rsplit("}", 1)[-1]


def direct_children(element, name):
    return [child for child in element if local_name(child.tag) == name]


def exactly_one(element, name, context):
    matches = direct_children(element, name)
    if len(matches) != 1:
        fail(f"{context} must contain exactly one {name} element; found {len(matches)}")
    return matches[0]


def require_scalar(element, context, allowed_attributes=()):
    if list(element):
        fail(f"{context} must not contain child elements")
    unexpected_attributes = set(element.attrib) - set(allowed_attributes)
    if unexpected_attributes:
        fail(
            f"{context} has unexpected attributes: "
            f"{sorted(unexpected_attributes)}"
        )


def validate_entry_name(name, package_label):
    if not name or "\x00" in name or "\\" in name or name.startswith("/"):
        fail(f"{package_label} has unsafe ZIP entry: {name!r}")

    name_without_directory_marker = name[:-1] if name.endswith("/") else name
    normalized_unicode = unicodedata.normalize("NFKC", name_without_directory_marker)
    if not normalized_unicode or re.match(r"^[A-Za-z]:", normalized_unicode):
        fail(f"{package_label} has unsafe Windows ZIP entry: {name!r}")

    path = pathlib.PurePosixPath(name_without_directory_marker)
    if any(part in ("", ".", "..") for part in path.parts):
        fail(f"{package_label} has unsafe ZIP entry: {name!r}")
    if any(":" in part or part.rstrip(" .") != part for part in path.parts):
        fail(f"{package_label} has unsafe Windows ZIP entry: {name!r}")
    normalized = posixpath.normpath(name_without_directory_marker)
    if normalized != name_without_directory_marker:
        fail(f"{package_label} has non-canonical ZIP entry: {name!r}")

    return unicodedata.normalize("NFKC", normalized).casefold()


def read_safe_archive(path, package_label):
    try:
        archive = zipfile.ZipFile(path, "r")
    except (OSError, zipfile.BadZipFile) as exc:
        fail(f"{package_label} is not a readable ZIP archive: {exc}")

    entries = {}
    canonical_entries = {}
    try:
        for item in archive.infolist():
            canonical_name = validate_entry_name(item.filename, package_label)
            if canonical_name in canonical_entries:
                existing = canonical_entries[canonical_name][0]
                fail(
                    f"{package_label} has duplicate or file-directory-colliding "
                    f"ZIP entries: {existing!r}, {item.filename!r}"
                )
            if item.flag_bits & 0x1:
                fail(f"{package_label} has encrypted ZIP entry: {item.filename}")

            unix_mode = item.external_attr >> 16
            file_type = stat.S_IFMT(unix_mode)
            if file_type == stat.S_IFLNK:
                fail(f"{package_label} has symbolic-link ZIP entry: {item.filename}")
            if file_type not in (0, stat.S_IFREG, stat.S_IFDIR):
                fail(f"{package_label} has special-file ZIP entry: {item.filename}")

            entries[item.filename] = item
            canonical_entries[canonical_name] = (item.filename, item.is_dir())

        canonical_files = {
            name for name, (_, is_directory) in canonical_entries.items()
            if not is_directory
        }
        for canonical_name, (original_name, _) in canonical_entries.items():
            parts = canonical_name.split("/")
            for index in range(1, len(parts)):
                parent = "/".join(parts[:index])
                if parent in canonical_files:
                    fail(
                        f"{package_label} has file-directory-colliding ZIP entries: "
                        f"{canonical_entries[parent][0]!r}, {original_name!r}"
                    )

        bad_crc = archive.testzip()
        if bad_crc is not None:
            fail(f"{package_label} has a corrupt ZIP entry: {bad_crc}")
    except (OSError, RuntimeError, zipfile.BadZipFile) as exc:
        archive.close()
        fail(f"{package_label} is malformed: {exc}")

    return archive, entries


def require_entry(entries, path, package_label):
    item = entries.get(path)
    if item is None or item.is_dir():
        fail(f"{package_label} is missing required file: {path}")
    return item


def normalized_tfm(value):
    lowered = value.casefold()
    if lowered.startswith(".netstandard"):
        return lowered[1:]
    return lowered


def validate_nuspec(archive, entries, library_tfms):
    nuspec_names = [name for name in entries if name.casefold().endswith(".nuspec")]
    expected_name = f"{ASSEMBLY}.nuspec"
    if nuspec_names != [expected_name]:
        fail(
            "nupkg must contain exactly one root nuspec named "
            f"{expected_name}; found {nuspec_names}"
        )

    try:
        root = ET.fromstring(archive.read(expected_name))
    except (ET.ParseError, KeyError, RuntimeError) as exc:
        fail(f"nuspec is malformed: {exc}")

    if local_name(root.tag) != "package":
        fail("nuspec root element must be package")
    metadata = exactly_one(root, "metadata", "nuspec package")

    package_id = exactly_one(metadata, "id", "nuspec metadata")
    require_scalar(package_id, "nuspec package id")
    if (package_id.text or "").strip() != ASSEMBLY:
        fail(f"nuspec package id must be exactly {ASSEMBLY}")

    version = exactly_one(metadata, "version", "nuspec metadata")
    require_scalar(version, "nuspec package version")
    if (version.text or "").strip() != EXPECTED_VERSION:
        fail(f"nuspec package version must be exactly {EXPECTED_VERSION}")

    repository = exactly_one(metadata, "repository", "nuspec metadata")
    require_scalar(repository, "nuspec repository", ("type", "url", "commit"))
    if (
        repository.attrib.get("url") != EXPECTED_REPOSITORY
        or repository.attrib.get("type") != "git"
        or (repository.text or "").strip()
    ):
        fail(f"nuspec repository URL must be exactly {EXPECTED_REPOSITORY}")
    commit = repository.attrib.get("commit")
    if commit is not None and not re.fullmatch(r"[a-f0-9]{40}", commit):
        fail("nuspec repository commit must be a lowercase 40-character Git SHA")

    license_element = exactly_one(metadata, "license", "nuspec metadata")
    require_scalar(license_element, "nuspec license", ("type",))
    if (
        set(license_element.attrib) != {"type"}
        or license_element.attrib.get("type") != "expression"
        or (license_element.text or "").strip() != "MIT"
    ):
        fail("nuspec license expression must be exactly MIT")

    dependencies = exactly_one(metadata, "dependencies", "nuspec metadata")
    groups = direct_children(dependencies, "group")
    if len(groups) != len(list(dependencies)):
        fail("nuspec dependencies must contain only target-framework groups")

    expected_groups = {normalized_tfm(tfm) for tfm in library_tfms}
    actual_groups = set()
    for group in groups:
        if set(group.attrib) != {"targetFramework"}:
            fail("nuspec dependency group must have only targetFramework")
        target = group.attrib.get("targetFramework")
        if target is None or not target.strip():
            fail("nuspec dependency group is missing targetFramework")
        normalized = normalized_tfm(target.strip())
        if normalized in actual_groups:
            fail(f"nuspec has duplicate dependency group: {target}")
        actual_groups.add(normalized)

        dependency_elements = list(group)
        if any(local_name(child.tag) != "dependency" for child in dependency_elements):
            fail(
                f"nuspec dependency group {target} must contain only direct "
                "dependency elements"
            )

        direct_dependency_ids = {id(child) for child in dependency_elements}
        for descendant in group.iter():
            if (
                descendant is not group
                and local_name(descendant.tag) == "dependency"
                and descendant.attrib.get("id", "").casefold() == TURNKEY.casefold()
                and id(descendant) not in direct_dependency_ids
            ):
                fail(
                    f"nuspec dependency group {target} contains a nested "
                    f"{TURNKEY} dependency"
                )

        for dependency_element in dependency_elements:
            if list(dependency_element):
                fail(
                    f"nuspec dependency {dependency_element.attrib.get('id', '<missing>')} "
                    f"in {target} must not contain child elements"
                )
            dependency_id = dependency_element.attrib.get("id")
            dependency_version = dependency_element.attrib.get("version")
            if not dependency_id or not dependency_version:
                fail(
                    f"nuspec dependency in {target} must have direct non-empty "
                    "id and version attributes"
                )

        turnkey_dependencies = [
            child
            for child in dependency_elements
            if child.attrib.get("id", "").casefold() == TURNKEY.casefold()
        ]
        if len(turnkey_dependencies) != 1:
            fail(
                f"nuspec dependency group {target} must contain exactly one "
                f"{TURNKEY} dependency; found {len(turnkey_dependencies)}"
            )
        dependency = turnkey_dependencies[0]
        if dependency.attrib.get("id") != TURNKEY:
            fail(f"nuspec dependency id must be exactly {TURNKEY}")
        if dependency.attrib.get("version") != EXPECTED_TURNKEY_RANGE:
            fail(
                f"nuspec {TURNKEY} dependency in {target} must be exactly "
                f"{EXPECTED_TURNKEY_RANGE}"
            )

    if actual_groups != expected_groups:
        fail(
            "nuspec dependency groups do not match package library TFMs: "
            f"expected {sorted(expected_groups)}, found {sorted(actual_groups)}"
        )


def validate():
    nupkg_archive, nupkg_entries = read_safe_archive(NUPKG, "nupkg")
    snupkg_archive, snupkg_entries = read_safe_archive(SNUPKG, "snupkg")
    try:
        fixed_paths = [
            "README.md",
            "LICENSE",
            f"lib/netstandard2.1/{ASSEMBLY}.dll",
            f"lib/netstandard2.1/{ASSEMBLY}.xml",
            f"lib/net8.0/{ASSEMBLY}.dll",
            f"lib/net8.0/{ASSEMBLY}.xml",
        ]
        for path in fixed_paths:
            require_entry(nupkg_entries, path, "nupkg")

        library_dlls = sorted(
            name
            for name, item in nupkg_entries.items()
            if not item.is_dir()
            and name.startswith("lib/")
            and name.endswith(f"/{ASSEMBLY}.dll")
            and len(pathlib.PurePosixPath(name).parts) == 3
        )
        library_tfms = [pathlib.PurePosixPath(path).parts[1] for path in library_dlls]
        required_tfms = {"netstandard2.1", "net8.0"}
        windows_tfms = {
            tfm for tfm in library_tfms
            if re.fullmatch(r"net8\.0-windows(?:[0-9]+(?:\.[0-9]+)*)?", tfm)
        }
        unsupported_tfms = set(library_tfms) - required_tfms - windows_tfms
        if unsupported_tfms:
            fail(
                "nupkg contains unsupported SDK library TFM(s): "
                f"{sorted(unsupported_tfms)}"
            )
        if len(windows_tfms) != 1:
            fail(
                "nupkg must contain exactly one net8.0-windows* SDK library TFM; "
                f"found {sorted(windows_tfms)}"
            )
        expected_tfms = required_tfms | windows_tfms
        if set(library_tfms) != expected_tfms or len(library_tfms) != len(expected_tfms):
            fail(
                "nupkg SDK library inventory must contain exactly one assembly per "
                f"supported TFM; expected {sorted(expected_tfms)}, "
                f"found {sorted(library_tfms)}"
            )

        validate_nuspec(nupkg_archive, nupkg_entries, library_tfms)

        for dll_path in library_dlls:
            pdb_path = dll_path.removesuffix(".dll") + ".pdb"
            require_entry(snupkg_entries, pdb_path, "snupkg")
    finally:
        nupkg_archive.close()
        snupkg_archive.close()


try:
    validate()
except ValidationError as exc:
    print(f"package validation failed: {exc}", file=sys.stderr)
    raise SystemExit(1)

print(f"package structure validation passed: {NUPKG} + {SNUPKG}")
PY

temporary_directory=$(mktemp -d)
trap 'rm -rf "$temporary_directory"' EXIT
export DOTNET_CLI_HOME="$temporary_directory/.dotnet"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export NUGET_PACKAGES="$temporary_directory/.nuget/packages"
public_nuget_config="$temporary_directory/nuget.config"
verifier_intermediate="$temporary_directory/obj/"
verifier_base_output="$temporary_directory/bin/"
verifier_output="$temporary_directory/verifier"
cat > "$public_nuget_config" <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
XML

dotnet restore "$verifier_project" \
  --configfile "$public_nuget_config" \
  -p:BaseIntermediateOutputPath="$verifier_intermediate" \
  -p:MSBuildProjectExtensionsPath="$verifier_intermediate" \
  -p:BaseOutputPath="$verifier_base_output"
dotnet build "$verifier_project" \
  --configuration Release \
  --no-restore \
  --output "$verifier_output" \
  -p:BaseIntermediateOutputPath="$verifier_intermediate" \
  -p:MSBuildProjectExtensionsPath="$verifier_intermediate" \
  -p:BaseOutputPath="$verifier_base_output"
dotnet "$verifier_output/KyuzanInc.Peak.Tools.PackageSymbolVerifier.dll" \
  "$nupkg" "$snupkg"

printf 'package validation passed: %s + %s\n' "$nupkg" "$snupkg"
