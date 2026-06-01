#!/usr/bin/env bash
# Regenerate the internal C# OpenAPI client from the pinned peak-server spec.
#
# This is the C# counterpart of peak's `pnpm --filter peak-server
# openapi:generate`: same engine (@openapitools/openapi-generator-cli, core
# pinned to 7.9.0 via openapitools.json), same source spec, emitting the
# `csharp` generator instead of typescript-axios. Drift CI runs this and
# fails if the committed client changes, so the output must stay
# deterministic (hideGenerationTimestamp=true in openapi-config.yaml).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PKG="$REPO_ROOT/packages/peak-public-api-client-csharp"
SPEC="$REPO_ROOT/upstream-snapshots/peak-server-openapi/public-api.yaml"

if [ ! -f "$SPEC" ]; then
  echo "ERROR: spec not found at $SPEC" >&2
  echo "Run: scripts/sync-upstream.sh peak-server-openapi main" >&2
  exit 1
fi

# clean:generated — drop only the generated sources; keep the hand-authored
# csproj / config / .openapi-generator-ignore that live alongside them.
rm -rf "$PKG/src/KyuzanInc.Peak.PublicApiClient/Api" \
       "$PKG/src/KyuzanInc.Peak.PublicApiClient/Client" \
       "$PKG/src/KyuzanInc.Peak.PublicApiClient/Model" \
       "$PKG/.openapi-generator"

# cd into the package so the generator reads its openapitools.json (pins the
# 7.9.0 core) and its package-lock.json. `npm ci` installs the generator
# wrapper and its npm deps from the committed lockfile — no live npm
# resolution — and the wrapper then fetches the version-pinned 7.9.0 jar.
cd "$PKG"
npm ci --silent
./node_modules/.bin/openapi-generator-cli generate \
  -g csharp \
  -i "$SPEC" \
  -o "$PKG" \
  -c "$PKG/openapi-config.yaml"

# Fail loud if the wrapper ever ran a different core than the pinned 7.9.0
# (e.g. a stale npx cache or a missing openapitools.json) — otherwise drift CI
# could silently compare output from two generator versions.
EXPECTED_VERSION="7.9.0"
ACTUAL_VERSION="$(cat "$PKG/.openapi-generator/VERSION" 2>/dev/null || echo "MISSING")"
if [ "$ACTUAL_VERSION" != "$EXPECTED_VERSION" ]; then
  echo "ERROR: generator core was $ACTUAL_VERSION, expected $EXPECTED_VERSION (check openapitools.json)." >&2
  exit 1
fi

echo "Generated C# client into $PKG/src/KyuzanInc.Peak.PublicApiClient/ (generator $ACTUAL_VERSION)"
