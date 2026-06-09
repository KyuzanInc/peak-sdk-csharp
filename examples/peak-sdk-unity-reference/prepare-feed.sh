#!/usr/bin/env bash
# Populate ./LocalFeed so the Unity project can restore KyuzanInc.Peak.Sdk and its
# transitive KyuzanInc.Turnkey.Sdk WITHOUT GitHub Packages auth at Unity-restore time.
# Prerequisite: run `dotnet restore peak-sdk-csharp.sln` once first (warms the NuGet
# cache with the Turnkey nupkg). See README for the GitHub-auth alternative.
set -euo pipefail
cd "$(dirname "$0")"

rm -f ./LocalFeed/KyuzanInc.Peak.Sdk.*.nupkg   # avoid resolving a stale SDK build
mkdir -p ./LocalFeed

# 1) Pack the working-tree SDK (csproj, NOT the solution) into the local feed.
dotnet pack ../../packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj -c Release -o ./LocalFeed

# 2) Vendor the EXACT pinned transitive Turnkey nupkg from the NuGet cache.
#    The version is pinned exact in Directory.Packages.props as [X]; read it, or
#    hardcode it if this extraction ever drifts (the pin changes rarely).
TK_VER="$(grep -oE 'KyuzanInc\.Turnkey\.Sdk"[^/]*Version="\[?[0-9][^]"]*' ../../Directory.Packages.props | grep -oE '[0-9][^]"]*$')"
: "${TK_VER:?could not read KyuzanInc.Turnkey.Sdk version from Directory.Packages.props}"
TK_NUPKG="${NUGET_PACKAGES:-$HOME/.nuget/packages}/kyuzaninc.turnkey.sdk/${TK_VER}/kyuzaninc.turnkey.sdk.${TK_VER}.nupkg"
if [ ! -f "$TK_NUPKG" ]; then
  echo "ERROR: $TK_NUPKG not found." >&2
  echo "Run 'dotnet restore peak-sdk-csharp.sln' once (with GitHub Packages auth) to warm the cache, then re-run." >&2
  exit 1
fi
cp "$TK_NUPKG" ./LocalFeed/

echo "LocalFeed ready:"
ls -1 ./LocalFeed
