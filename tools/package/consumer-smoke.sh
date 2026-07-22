#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "usage: $0 <peak-feed> <turnkey-feed> <tfm>" >&2
  exit 1
fi

peak_feed=$1
turnkey_feed=$2
tfm=$3

case "$tfm" in
  netstandard2.1|net8.0)
    ;;
  *)
    echo "unsupported consumer target framework: $tfm" >&2
    exit 1
    ;;
esac

for tool in dotnet python3; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "required tool is unavailable: $tool" >&2
    exit 1
  fi
done

if [[ ! -d "$peak_feed" ]]; then
  echo "Peak package feed is not a directory: $peak_feed" >&2
  exit 1
fi
if [[ ! -d "$turnkey_feed" ]]; then
  echo "Turnkey package feed is not a directory: $turnkey_feed" >&2
  exit 1
fi

peak_feed=$(cd "$peak_feed" && pwd -P)
turnkey_feed=$(cd "$turnkey_feed" && pwd -P)

consumer_directory=$(mktemp -d)
trap 'rm -rf "$consumer_directory"' EXIT
export DOTNET_CLI_HOME="$consumer_directory/.dotnet"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export NUGET_PACKAGES="$consumer_directory/.nuget/packages"
project="$consumer_directory/Consumer.csproj"
source="$consumer_directory/Program.cs"
nuget_config="$consumer_directory/NuGet.Config"

python3 - "$peak_feed" "$turnkey_feed" "$nuget_config" <<'PY'
import html
import pathlib
import sys

peak_feed, turnkey_feed, config_path = sys.argv[1:]
config = f'''<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="peak-local" value="{html.escape(peak_feed, quote=True)}" />
    <add key="turnkey-local" value="{html.escape(turnkey_feed, quote=True)}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="peak-local">
      <package pattern="KyuzanInc.Peak.*" />
    </packageSource>
    <packageSource key="turnkey-local">
      <package pattern="KyuzanInc.Turnkey.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="BouncyCastle.*" />
      <package pattern="Microsoft.*" />
      <package pattern="NETStandard.Library" />
      <package pattern="System.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
'''
pathlib.Path(config_path).write_text(config, encoding="utf-8")
PY

if [[ "$tfm" == netstandard2.1 ]]; then
  cat > "$project" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>11</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="KyuzanInc.Peak.Sdk" Version="[1.0.1]" />
  </ItemGroup>
</Project>
XML

  cat > "$source" <<'CS'
using KyuzanInc.Peak.Sdk;
using KyuzanInc.Peak.Sdk.Storage;

public static class ConsumerSmoke
{
    public static bool RoundTrip()
    {
        var storage = new InMemoryStorage();
        var client = PeakClient.Initialize(new PeakClientOptions
        {
            ApiUrl = "https://api.example.com",
            ProjectApiKey = "consumer-smoke",
            Storage = storage,
        });
        storage.Set("smoke-key", "smoke-value");
        return client is not null && storage.Get("smoke-key") == "smoke-value";
    }
}
CS
else
  cat > "$project" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>11</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="KyuzanInc.Peak.Sdk" Version="[1.0.1]" />
  </ItemGroup>
</Project>
XML

  cat > "$source" <<'CS'
using System;
using KyuzanInc.Peak.Sdk;
using KyuzanInc.Peak.Sdk.Storage;

var storage = new InMemoryStorage();
var client = PeakClient.Initialize(new PeakClientOptions
{
    ApiUrl = "https://api.example.com",
    ProjectApiKey = "consumer-smoke",
    Storage = storage,
});
storage.Set("smoke-key", "smoke-value");
if (client is null || storage.Get("smoke-key") != "smoke-value")
{
    throw new InvalidOperationException("Peak consumer storage round-trip failed");
}
Console.WriteLine("Peak consumer storage round-trip passed");
CS
fi

dotnet restore "$project" --configfile "$nuget_config" --no-cache --force-evaluate

assets="$consumer_directory/obj/project.assets.json"
python3 - "$assets" <<'PY'
import json
import pathlib
import sys

assets_path = pathlib.Path(sys.argv[1])
try:
    assets = json.loads(assets_path.read_text(encoding="utf-8"))
except (OSError, UnicodeError, json.JSONDecodeError) as exc:
    print(f"consumer assets file is unreadable: {exc}", file=sys.stderr)
    raise SystemExit(1)

libraries = assets.get("libraries")
if not isinstance(libraries, dict):
    print("consumer assets file has no libraries object", file=sys.stderr)
    raise SystemExit(1)

expected = {
    "KyuzanInc.Peak.Sdk": "KyuzanInc.Peak.Sdk/1.0.1",
    "KyuzanInc.Turnkey.Sdk": "KyuzanInc.Turnkey.Sdk/1.0.0",
}
for package_id, identity in expected.items():
    matches = [key for key in libraries if key.split("/", 1)[0].casefold() == package_id.casefold()]
    if matches != [identity]:
        print(
            f"consumer assets require exact identity {identity}; found {matches}",
            file=sys.stderr,
        )
        raise SystemExit(1)
PY

dotnet build "$project" -c Release --no-restore
if [[ "$tfm" == net8.0 ]]; then
  dotnet run --project "$project" -c Release --no-build --no-restore
fi

printf 'consumer smoke passed: %s (Peak 1.0.1, Turnkey 1.0.0)\n' "$tfm"
