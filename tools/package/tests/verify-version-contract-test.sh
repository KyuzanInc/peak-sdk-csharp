#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
verifier="$repo_root/tools/package/verify-version-contract.sh"

if [[ ! -f "$verifier" ]]; then
  echo "version-contract verifier is missing: $verifier" >&2
  exit 1
fi

fixture=$(mktemp -d)
trap 'rm -rf "$fixture"' EXIT

mkdir -p "$fixture/packages/peak-sdk-csharp/src" "$fixture/packages/peak-sdk-csharp/tests"
git -C "$fixture" init -q
git -C "$fixture" config user.email fixture@example.com
git -C "$fixture" config user.name Fixture

write_valid_contract() {
  cat > "$fixture/Directory.Packages.props" <<XML
<Project>
  <ItemGroup>
    <PackageVersion Include="KyuzanInc.Turnkey.Sdk" Version="[1.0.0]" />
  </ItemGroup>
</Project>
XML

  cat > "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <Version>1.0.0</Version>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="KyuzanInc.Turnkey.Sdk" />
  </ItemGroup>
</Project>
XML

  cat > "$fixture/packages/peak-sdk-csharp/src/packages.lock.json" <<JSON
{
  "version": 1,
  "dependencies": {
    "net8.0": {
      "KyuzanInc.Turnkey.Sdk": {
        "type": "Direct",
        "requested": "[1.0.0, 1.0.0]",
        "resolved": "1.0.0",
        "contentHash": "I3vQF6qcpQh2rpxnPQ7FzrcyS80vT8Rv8DDD4h8YrBAROpfRq3LuHiAqXTsjeT2gK4XQcEMfm4G8yr0CFoqSng=="
      }
    }
  }
}
JSON

  cat > "$fixture/packages/peak-sdk-csharp/tests/packages.lock.json" <<JSON
{
  "version": 1,
  "dependencies": {
    "net8.0": {
      "KyuzanInc.Peak.Sdk": {
        "type": "Project",
        "dependencies": {
          "KyuzanInc.Turnkey.Sdk": "[1.0.0, 1.0.0]"
        }
      },
      "KyuzanInc.Turnkey.Sdk": {
        "type": "CentralTransitive",
        "requested": "[1.0.0, 1.0.0]",
        "resolved": "1.0.0",
        "contentHash": "I3vQF6qcpQh2rpxnPQ7FzrcyS80vT8Rv8DDD4h8YrBAROpfRq3LuHiAqXTsjeT2gK4XQcEMfm4G8yr0CFoqSng=="
      }
    }
  }
}
JSON

  git -C "$fixture" add -A
}

accepted_bypasses=0

run_expect_fail() {
  local description=$1
  if VERSION_CONTRACT_REPO_ROOT="$fixture" bash "$verifier" >/dev/null 2>&1; then
    echo "version-contract bypass was accepted: $description" >&2
    accepted_bypasses=$((accepted_bypasses + 1))
  fi
}

run_expect_fail_with_message() {
  local description=$1
  local expected_message=$2
  local output

  if output=$(VERSION_CONTRACT_REPO_ROOT="$fixture" bash "$verifier" 2>&1); then
    echo "version-contract bypass was accepted: $description" >&2
    accepted_bypasses=$((accepted_bypasses + 1))
  elif ! grep -Fq "$expected_message" <<< "$output"; then
    echo "version-contract rejection lacked package-identity evidence: $description" >&2
    accepted_bypasses=$((accepted_bypasses + 1))
  fi
}

write_valid_contract
sed -i.bak 's#<Version>1.0.0</Version>#<Version>0.1.0-alpha.3</Version>#' \
  "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj"
rm -f "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj.bak"
git -C "$fixture" add -A
run_expect_fail 'stale Peak version'

write_valid_contract
sed -i.bak 's#Version="\[1.0.0\]"#Version="[0.1.0-alpha.0]"#' "$fixture/Directory.Packages.props"
rm -f "$fixture/Directory.Packages.props.bak"
git -C "$fixture" add -A
run_expect_fail 'stale Turnkey declaration'

write_valid_contract
sed -i.bak 's#Version="\[1.0.0\]"#Version="[1.0.0,2.0.0)"#' "$fixture/Directory.Packages.props"
rm -f "$fixture/Directory.Packages.props.bak"
git -C "$fixture" add -A
run_expect_fail 'floating Turnkey declaration'

write_valid_contract
cat > "$fixture/Directory.Packages.props" <<'XML'
<Project>
  <!-- <PackageVersion Include="KyuzanInc.Turnkey.Sdk" Version="[1.0.0]" /> -->
</Project>
XML
git -C "$fixture" add -A
run_expect_fail 'comment-only Turnkey declaration'

write_valid_contract
cat > "$fixture/Directory.Packages.props" <<'XML'
<Project><ItemGroup><PackageVersion Include="KyuzanInc.Turnkey.Sdk" Version="[1.0.0]" /><PackageVersion Include="KyuzanInc.Turnkey.Sdk" Version="[1.0.0]" /></ItemGroup></Project>
XML
git -C "$fixture" add -A
run_expect_fail 'same-line duplicate Turnkey declarations'

write_valid_contract
sed -i.bak '/PackageVersion/a\
    <PackageVersion Include="KyuzanInc.Turnkey.Sdk" Version="[1.0.0]" />' \
  "$fixture/Directory.Packages.props"
rm -f "$fixture/Directory.Packages.props.bak"
git -C "$fixture" add -A
run_expect_fail 'ordinary duplicate Turnkey declarations'

write_valid_contract
sed -i.bak '/PackageVersion/a\
    <PackageVersion Update="KyuzanInc.Turnkey.Sdk" Version="[1.0.0, 2.0.0)" />' \
  "$fixture/Directory.Packages.props"
rm -f "$fixture/Directory.Packages.props.bak"
git -C "$fixture" add -A
run_expect_fail 'floating central Update override'

write_valid_contract
sed -i.bak '/PackageVersion/a\
    <PackageVersion Remove="KyuzanInc.Turnkey.Sdk" />' \
  "$fixture/Directory.Packages.props"
rm -f "$fixture/Directory.Packages.props.bak"
git -C "$fixture" add -A
run_expect_fail 'central Remove override'

write_valid_contract
sed -i.bak '/PackageVersion/a\
    <PackageVersion Include="kyuzaninc.turnkey.sdk" Version="[1.0.0, 2.0.0)" />' \
  "$fixture/Directory.Packages.props"
rm -f "$fixture/Directory.Packages.props.bak"
git -C "$fixture" add -A
run_expect_fail 'case-variant central declaration'

write_valid_contract
sed -i.bak '/PackageVersion/a\
    <PackageVersion Update="KyuzanInc.Turnkey.*" Version="[1.0.0, 2.0.0)" />' \
  "$fixture/Directory.Packages.props"
rm -f "$fixture/Directory.Packages.props.bak"
git -C "$fixture" add -A
run_expect_fail 'wildcard central Update override'

write_valid_contract
cat > "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><Version>1.0.0</Version></PropertyGroup>
  <ItemGroup>
    <!-- <PackageReference Include="KyuzanInc.Turnkey.Sdk" /> -->
  </ItemGroup>
</Project>
XML
git -C "$fixture" add -A
run_expect_fail 'comment-only Turnkey PackageReference'

write_valid_contract
cat > "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><Version>1.0.0</Version></PropertyGroup><ItemGroup><PackageReference Include="KyuzanInc.Turnkey.Sdk" /><PackageReference Include="KyuzanInc.Turnkey.Sdk" /></ItemGroup></Project>
XML
git -C "$fixture" add -A
run_expect_fail 'same-line duplicate Turnkey PackageReferences'

write_valid_contract
sed -i.bak '/PackageReference/a\
    <PackageReference Include="KyuzanInc.Turnkey.Sdk" />' \
  "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj"
rm -f "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj.bak"
git -C "$fixture" add -A
run_expect_fail 'ordinary duplicate Turnkey PackageReferences'

write_valid_contract
sed -i.bak 's#Include="KyuzanInc.Turnkey.Sdk" />#Include="KyuzanInc.Turnkey.Sdk" Version="[1.0.0]" />#' \
  "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj"
rm -f "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj.bak"
git -C "$fixture" add -A
run_expect_fail 'PackageReference Version override'

write_valid_contract
sed -i.bak 's#Include="KyuzanInc.Turnkey.Sdk" />#Include="KyuzanInc.Turnkey.Sdk" VersionOverride="[1.0.0]" />#' \
  "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj"
rm -f "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj.bak"
git -C "$fixture" add -A
run_expect_fail 'PackageReference VersionOverride'

write_valid_contract
sed -i.bak '/PackageReference/a\
    <PackageReference Update="KyuzanInc.Turnkey.Sdk" Version="[1.0.0]" />' \
  "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj"
rm -f "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj.bak"
git -C "$fixture" add -A
run_expect_fail 'PackageReference Update Version'

write_valid_contract
sed -i.bak '/PackageReference/a\
    <PackageReference Update="KyuzanInc.Turnkey.Sdk" VersionOverride="[1.0.0]" />' \
  "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj"
rm -f "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj.bak"
git -C "$fixture" add -A
run_expect_fail 'PackageReference Update VersionOverride'

write_valid_contract
sed -i.bak '/PackageReference/a\
    <PackageReference Remove="KyuzanInc.Turnkey.Sdk" />' \
  "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj"
rm -f "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj.bak"
git -C "$fixture" add -A
run_expect_fail 'PackageReference Remove'

write_valid_contract
sed -i.bak '/PackageReference/a\
    <PackageReference Include="KYUZANINC.TURNKEY.SDK" Version="[1.0.0]" />' \
  "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj"
rm -f "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj.bak"
git -C "$fixture" add -A
run_expect_fail 'case-variant PackageReference'

write_valid_contract
sed -i.bak '/PackageReference/a\
    <PackageReference Remove="KyuzanInc.Turnkey.*" />' \
  "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj"
rm -f "$fixture/packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj.bak"
git -C "$fixture" add -A
run_expect_fail 'wildcard PackageReference Remove'

write_valid_contract
sed -i.bak '/"requested":/d' "$fixture/packages/peak-sdk-csharp/src/packages.lock.json"
rm -f "$fixture/packages/peak-sdk-csharp/src/packages.lock.json.bak"
git -C "$fixture" add -A
run_expect_fail 'missing requested lock field'

write_valid_contract
sed -i.bak '/"resolved":/d' "$fixture/packages/peak-sdk-csharp/src/packages.lock.json"
rm -f "$fixture/packages/peak-sdk-csharp/src/packages.lock.json.bak"
git -C "$fixture" add -A
run_expect_fail 'missing resolved lock field'

write_valid_contract
sed -i.bak 's#I3vQF6qcpQh2rpxnPQ7FzrcyS80vT8Rv8DDD4h8YrBAROpfRq3LuHiAqXTsjeT2gK4XQcEMfm4G8yr0CFoqSng==#rebuilt-package-hash#' \
  "$fixture/packages/peak-sdk-csharp/src/packages.lock.json"
rm -f "$fixture/packages/peak-sdk-csharp/src/packages.lock.json.bak"
git -C "$fixture" add -A
run_expect_fail 'non-published Turnkey package content hash'

write_valid_contract
sed -i.bak 's#"KyuzanInc.Turnkey.Sdk": "\[1.0.0, 1.0.0\]"#"KyuzanInc.Turnkey.Sdk": "[1.0.0, 2.0.0)"#' \
  "$fixture/packages/peak-sdk-csharp/tests/packages.lock.json"
rm -f "$fixture/packages/peak-sdk-csharp/tests/packages.lock.json.bak"
git -C "$fixture" add -A
run_expect_fail 'floating nested dependency range'

write_valid_contract
cat > "$fixture/packages/peak-sdk-csharp/src/packages.lock.json" <<'JSON'
{
  "version": 1,
  "dependencies": {
    "net8.0": {
      "KyuzanInc.Turnkey.Sdk": {
        "type": "Direct",
        "requested": "[1.0.0, 1.0.0]",
        "resolved": "1.0.0"
      },
      "kyuzaninc.turnkey.sdk": {
        "type": "Direct",
        "resolved": "0.1.0-alpha.0"
      }
    }
  }
}
JSON
git -C "$fixture" add -A
run_expect_fail_with_message \
  'canonical plus case-variant lock identities' \
  'multiple case-insensitive Turnkey package keys'

write_valid_contract
sed -i.bak 's#"KyuzanInc.Turnkey.Sdk": "\[1.0.0, 1.0.0\]"#"kyuzaninc.turnkey.sdk": "[1.0.0, 2.0.0)"#' \
  "$fixture/packages/peak-sdk-csharp/tests/packages.lock.json"
rm -f "$fixture/packages/peak-sdk-csharp/tests/packages.lock.json.bak"
git -C "$fixture" add -A
run_expect_fail_with_message \
  'case-variant nested dependency key' \
  'noncanonical Turnkey package key'

write_valid_contract
sed -i.bak 's/"KyuzanInc.Turnkey.Sdk": {/"KYUZANINC.TURNKEY.SDK": {/' \
  "$fixture/packages/peak-sdk-csharp/src/packages.lock.json"
rm -f "$fixture/packages/peak-sdk-csharp/src/packages.lock.json.bak"
git -C "$fixture" add -A
run_expect_fail_with_message \
  'case-variant-only lock object' \
  'noncanonical Turnkey package key'

write_valid_contract
rm "$fixture/packages/peak-sdk-csharp/src/packages.lock.json"
git -C "$fixture" add -A
run_expect_fail 'missing SDK source lock file'

write_valid_contract
rm "$fixture/packages/peak-sdk-csharp/tests/packages.lock.json"
git -C "$fixture" add -A
run_expect_fail 'missing SDK test lock file'

write_valid_contract
VERSION_CONTRACT_REPO_ROOT="$fixture" bash "$verifier"

ambient_config="$fixture/NuGet.Config"
cat > "$ambient_config" <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="private-unreachable" value="https://127&#46;0&#46;0&#46;1:9/private/index.json" />
  </packageSources>
</configuration>
XML

dotnet_shim_directory="$fixture/dotnet-shim"
dotnet_log="$fixture/dotnet-calls.log"
dotnet_marker="$fixture/credential-free-restore-complete"
mkdir -p "$dotnet_shim_directory"
cat > "$dotnet_shim_directory/dotnet" <<'SH'
#!/usr/bin/env bash
set -euo pipefail

printf '%s\n' "$*" >> "$VERSION_CONTRACT_DOTNET_LOG"

case ${1:-} in
  restore)
    config_file=
    previous=
    for argument in "$@"; do
      if [[ "$previous" == '--configfile' ]]; then
        config_file=$argument
        break
      fi
      previous=$argument
    done

    if [[ -z "$config_file" || ! -f "$config_file" ]]; then
      echo 'restore did not use an explicit NuGet configuration' >&2
      exit 1
    fi
    if grep -Eiq 'nuget\.pkg\.github\.com|private-unreachable|127\.0\.0\.1' "$config_file"; then
      echo 'restore configuration contains a private or ambient source' >&2
      exit 1
    fi
    : > "$VERSION_CONTRACT_DOTNET_MARKER"
    ;;
  run)
    if [[ ! -f "$VERSION_CONTRACT_DOTNET_MARKER" ]]; then
      echo 'verifier ran without an isolated restore' >&2
      exit 1
    fi
    if [[ " $* " != *' --no-restore '* ]]; then
      echo 'verifier run did not disable implicit restore' >&2
      exit 1
    fi
    echo 'version-contract verification passed'
    ;;
  *)
    echo "unexpected dotnet command: ${1:-<missing>}" >&2
    exit 1
    ;;
esac
SH
chmod +x "$dotnet_shim_directory/dotnet"

if ! PATH="$dotnet_shim_directory:$PATH" \
  VERSION_CONTRACT_DOTNET_LOG="$dotnet_log" \
  VERSION_CONTRACT_DOTNET_MARKER="$dotnet_marker" \
  VERSION_CONTRACT_REPO_ROOT="$fixture" \
  bash "$verifier"; then
  echo 'ambient NuGet restore isolation was not enforced' >&2
  accepted_bypasses=$((accepted_bypasses + 1))
fi

if (( accepted_bypasses )); then
  echo "$accepted_bypasses version-contract bypass regression(s) were accepted" >&2
  exit 1
fi

echo "verify-version-contract regression tests passed"
