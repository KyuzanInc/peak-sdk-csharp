#!/usr/bin/env bash
set -euo pipefail

repo_root=${VERSION_CONTRACT_REPO_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}
verifier_project=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/VersionContractVerifier/VersionContractVerifier.csproj
expected_locks=(
  packages/peak-sdk-csharp/src/packages.lock.json
  packages/peak-sdk-csharp/tests/packages.lock.json
)

if ! command -v dotnet >/dev/null 2>&1; then
  echo "required tool is unavailable: dotnet" >&2
  exit 1
fi

if ! git -C "$repo_root" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo "VERSION_CONTRACT_REPO_ROOT is not a Git worktree: $repo_root" >&2
  exit 1
fi

for lock_file in "${expected_locks[@]}"; do
  if ! git -C "$repo_root" ls-files --error-unmatch "$lock_file" >/dev/null 2>&1; then
    echo "required committed lock file is missing: $lock_file" >&2
    exit 1
  fi
done

temporary_directory=$(mktemp -d)
trap 'rm -rf "$temporary_directory"' EXIT
public_nuget_config="$temporary_directory/nuget.config"
cat > "$public_nuget_config" <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
XML

dotnet restore "$verifier_project" --configfile "$public_nuget_config"
dotnet run \
  --project "$verifier_project" \
  --configuration Release \
  --no-restore \
  -- "$repo_root"
