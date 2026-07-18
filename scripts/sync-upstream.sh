#!/usr/bin/env bash
# sync-upstream.sh — re-snapshot one upstream source into upstream-snapshots/
#
# Usage:
#   scripts/sync-upstream.sh peak-server-openapi <pin>
#
# The only accepted <name> is peak-server-openapi. The pin is normally main;
# the script records the resolved HEAD commit.
#
# The script writes the raw upstream contract and resolved pin into the working
# tree only. It does not create/switch branches, stage files, or commit. The
# operator sanitizes and verifies the full output before touching the index.
#
# Turnkey crypto (Crypto / ApiKeyStamper / Http / Encoding) lives in
# KyuzanInc/turnkey-sdk-csharp and is consumed as the
# KyuzanInc.Turnkey.Sdk NuGet package; resyncing its sources is done
# from that repo, not this one. See docs/sync-rules.md.

set -euo pipefail

sha256_file() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{print tolower($1)}'
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$1" | awk '{print tolower($1)}'
  elif command -v openssl >/dev/null 2>&1; then
    openssl dgst -sha256 "$1" | awk '{print tolower($NF)}'
  else
    echo "ERROR: no SHA-256 command is available" >&2
    return 1
  fi
}

if [ "$#" -ne 2 ]; then
  echo "Usage: $0 peak-server-openapi <pin>" >&2
  exit 2
fi

NAME="$1"
PIN="$2"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DEST="$REPO_ROOT/upstream-snapshots/$NAME"

cd "$REPO_ROOT"

case "$NAME" in
  peak-server-openapi)
    SRC_REPO="git@github.com:KyuzanInc/peak.git"
    ;;
  *)
    echo "ERROR: unknown upstream name '$NAME'" >&2
    exit 1
    ;;
esac

echo "Snapshotting $NAME @ $PIN from $SRC_REPO"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

git clone --quiet "$SRC_REPO" "$TMP/clone"
cd "$TMP/clone"
git checkout --quiet "$PIN"

case "$NAME" in
  peak-server-openapi)
    # Replace the snapshot wholesale so a stale file from an older spec layout
    # cannot survive a resync. Only the spec + PIN.md belong here; both are
    # rewritten below.
    rm -rf "$DEST"
    mkdir -p "$DEST"
    cp "apps/peak-public-docs/docs/api-references/public-api.yaml" "$DEST/public-api.yaml"
    # Record the RESOLVED HEAD commit (reproducible) while tracking the ref
    # given as $PIN (e.g. `main`). Writing the literal $PIN would leave an
    # unpinned branch name behind, so resolve it to a SHA here.
    RESOLVED_COMMIT="$(git rev-parse HEAD)"
    IMPORTED_SHA256="$(sha256_file "$DEST/public-api.yaml")"
    cat > "$DEST/PIN.md" <<EOF
# peak-server OpenAPI snapshot

Tracks **\`$PIN\`** of \`KyuzanInc/peak\`. Each resync grabs that ref's current
HEAD and records the exact commit below, so the snapshot stays a reproducible,
pinned artifact while following the ref's latest.

| Tracks | Last synced commit | Snapshot date | Source path |
|---|---|---|---|
| \`$PIN\` | \`$RESOLVED_COMMIT\` | $(date -u +%Y-%m-%d) | KyuzanInc/peak \`apps/peak-public-docs/docs/api-references/public-api.yaml\` |

> Re-sync with \`scripts/sync-upstream.sh peak-server-openapi $PIN\`. The copied
> contract is a raw working-tree input. Do not stage it until the sanitization,
> regeneration, checksum, and public-tree gate steps in \`docs/sync-rules.md\`
> have all completed successfully.
EOF
    ;;
esac

cd "$REPO_ROOT"

echo
echo "Raw OpenAPI input copied to the working tree; the repository index was not modified."
echo "Resolved commit: $RESOLVED_COMMIT"
echo "Imported raw contract SHA-256: $IMPORTED_SHA256"
echo
echo "Before staging anything:"
echo "  1. Record the resolved commit and imported checksum in upstream-snapshots/SOURCES.md"
echo "     and docs/compatibility/upstream-pins.md."
echo "  2. Apply every deterministic substitution to public-api.yaml:"
echo "     - servers: one https://api.example.invalid/ entry"
echo "     - Stamp.stampHeaderValue example: synthetic-stamp"
echo "     - SignedRequest organizationId: 00000000-0000-0000-0000-000000000000"
echo "     - every 9c7076e7-c8d4-4b7b-a484-e9ed28f3931d example:"
echo "       00000000-0000-0000-0000-000000000000"
echo "     - Google callback URL: https://wallet.example.invalid/auth/google/callback"
echo "     - Google OIDC token example: synthetic.jwt.token"
echo "  3. Run ./scripts/generate-public-api-client.sh."
echo "  4. Calculate the sanitized contract SHA-256; write the exact one-line checksum"
echo "     manifest and the same public digest in docs/compatibility/upstream-pins.md."
echo "  5. Run bash tools/publication/verify-public-tree.sh and require PASS."
echo "  6. Only after PASS, stage every sanitized/generated/provenance/checksum output:"
echo "     git add -- upstream-snapshots/peak-server-openapi/public-api.yaml upstream-snapshots/peak-server-openapi/PIN.md upstream-snapshots/SOURCES.md docs/compatibility/upstream-pins.md tests/UpstreamSources/peak-server-openapi-public-api.sha256 packages/peak-public-api-client-csharp/src packages/peak-public-api-client-csharp/.openapi-generator"
echo "  7. Inspect git diff --cached --check and git diff --cached before committing."
echo
echo "Raw input lives at upstream-snapshots/$NAME/. Follow docs/sync-rules.md."
