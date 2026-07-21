#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
version=1.0.0
commit=5c219364d678c3a72786a2a89fbc7bbd4931be5e
expected_sha=9ea5b43e72b0c519f886b03375b59c3f36dbf2d6d2746b258a3adc1b5af672bb
url="https://github.com/KyuzanInc/turnkey-sdk-csharp/archive/${commit}.tar.gz"
archive_name="turnkey-sdk-csharp-${commit}.tar.gz"
manifest="$repo_root/tests/UpstreamSources/turnkey-sdk-csharp-1.0.0.sha256"
output_directory=${1:-"$repo_root/.artifacts/turnkey-feed"}
source_output_directory=${2:-"$repo_root/.artifacts/turnkey-source"}

if [[ $# -gt 2 || -z "$output_directory" || -z "$source_output_directory" ]]; then
  echo "usage: $0 [output-directory] [source-output-directory]" >&2
  exit 1
fi

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

for tool in awk dotnet tar; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "required tool is unavailable: $tool" >&2
    exit 1
  fi
done

if [[ ! -f "$manifest" ]]; then
  echo "Turnkey checksum manifest is missing: $manifest" >&2
  exit 1
fi

manifest_line_count=$(awk 'NF {count++} END {print count + 0}' "$manifest")
manifest_record=$(awk 'NF {print; exit}' "$manifest")
read -r manifest_sha manifest_archive extra <<< "$manifest_record" || true
manifest_sha=$(printf '%s' "${manifest_sha:-}" | tr '[:upper:]' '[:lower:]')

if [[ "$manifest_line_count" != 1 || ! "$manifest_sha" =~ ^[a-f0-9]{64}$ ||
      "$manifest_archive" != "$archive_name" || -n ${extra:-} ]]; then
  echo "invalid Turnkey checksum manifest: $manifest" >&2
  exit 1
fi

if [[ "$manifest_sha" != "$expected_sha" ]]; then
  echo "Turnkey checksum manifest does not match the pinned digest" >&2
  exit 1
fi

temporary_directory=$(mktemp -d)
destination_temporary_file=
source_staging_directory=
cleanup() {
  rm -rf "$temporary_directory"
  if [[ -n "$destination_temporary_file" ]]; then
    rm -f -- "$destination_temporary_file"
  fi
  if [[ -n "$source_staging_directory" ]]; then
    rm -rf -- "$source_staging_directory"
  fi
}
trap cleanup EXIT
archive="$temporary_directory/$archive_name"

if [[ -n ${TURNKEY_SOURCE_ARCHIVE:-} ]]; then
  if [[ ! -f "$TURNKEY_SOURCE_ARCHIVE" ]]; then
    echo "TURNKEY_SOURCE_ARCHIVE is not a file: $TURNKEY_SOURCE_ARCHIVE" >&2
    exit 1
  fi
  cp "$TURNKEY_SOURCE_ARCHIVE" "$archive"
else
  if ! command -v curl >/dev/null 2>&1; then
    echo "required tool is unavailable: curl" >&2
    exit 1
  fi
  curl --fail --location --silent --show-error "$url" --output "$archive"
fi

actual_sha=$(sha256 "$archive")
if [[ "$actual_sha" != "$expected_sha" || "$actual_sha" != "$manifest_sha" ]]; then
  echo "Turnkey source archive checksum mismatch: expected $expected_sha, got $actual_sha" >&2
  exit 1
fi

source_directory="$temporary_directory/turnkey-sdk-csharp-$commit"
tar -xzf "$archive" -C "$temporary_directory"
project="$source_directory/src/turnkey-sdk-csharp.csproj"
if [[ ! -f "$project" ]]; then
  echo "Turnkey source archive is missing src/turnkey-sdk-csharp.csproj" >&2
  exit 1
fi

if [[ -e "$source_output_directory" || -L "$source_output_directory" ]]; then
  echo "source output already exists: $source_output_directory" >&2
  exit 1
fi

source_output_parent=$(dirname "$source_output_directory")
mkdir -p "$source_output_parent"
source_staging_directory=$(mktemp -d "$source_output_parent/.turnkey-source.XXXXXX")
cp -R "$source_directory/." "$source_staging_directory/"
source_directory="$source_staging_directory"
project="$source_directory/src/turnkey-sdk-csharp.csproj"

public_nuget_config="$temporary_directory/nuget.public.config"
cat > "$public_nuget_config" <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
XML

dotnet restore "$project" --locked-mode --configfile "$public_nuget_config"

staging_feed="$temporary_directory/package"
mkdir -p "$staging_feed"
dotnet pack "$project" -c Release --no-restore --output "$staging_feed"

package_name="KyuzanInc.Turnkey.Sdk.${version}.nupkg"
staged_package="$staging_feed/$package_name"
staged_count=$(find "$staging_feed" -maxdepth 1 -type f -name '*.nupkg' -print | wc -l | tr -d '[:space:]')
if [[ "$staged_count" != 1 || ! -f "$staged_package" ]]; then
  echo "Turnkey pack did not produce exactly $package_name" >&2
  exit 1
fi

mkdir -p "$output_directory"
unexpected_count=$(find "$output_directory" -maxdepth 1 -name '*.nupkg' ! -name "$package_name" -print | wc -l | tr -d '[:space:]')
if [[ "$unexpected_count" != 0 ]]; then
  echo "output directory contains unexpected NuGet packages: $output_directory" >&2
  exit 1
fi

destination_package="$output_directory/$package_name"
if [[ -L "$destination_package" ||
      ( -e "$destination_package" && ! -f "$destination_package" ) ]]; then
  echo "refusing unsafe existing package target: $destination_package" >&2
  exit 1
fi

destination_temporary_file=$(mktemp "$output_directory/.${package_name}.tmp.XXXXXX")
cp "$staged_package" "$destination_temporary_file"
mv -f "$destination_temporary_file" "$destination_package"
destination_temporary_file=

final_count=$(find "$output_directory" -maxdepth 1 -name '*.nupkg' -print | wc -l | tr -d '[:space:]')
if [[ "$final_count" != 1 || ! -f "$destination_package" || -L "$destination_package" ]]; then
  echo "output directory does not contain exactly one NuGet package" >&2
  exit 1
fi

if [[ -e "$source_output_directory" || -L "$source_output_directory" ]]; then
  echo "source output appeared during preparation: $source_output_directory" >&2
  exit 1
fi
mv "$source_staging_directory" "$source_output_directory"
source_staging_directory=
project="$source_output_directory/src/turnkey-sdk-csharp.csproj"

printf 'Turnkey source archive SHA-256: %s\n' "$actual_sha"
printf 'Turnkey source project: %s\n' "$project"
printf 'Turnkey local package: %s\n' "$output_directory/$package_name"
