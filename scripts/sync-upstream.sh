#!/usr/bin/env bash
# sync-upstream.sh — re-snapshot one upstream source into upstream-snapshots/
#
# Usage:
#   scripts/sync-upstream.sh <name> <pin>
#
# Where <name> is one of:
#   peak-sdk-unity          — KyuzanInc/peak-sdk-unity, pin = commit SHA
#   turnkey-sdk-unity       — KyuzanInc/turnkey-sdk-unity, pin = commit SHA
#   tkhq-sdk                — tkhq/sdk (TypeScript), pin = version tag (e.g. crypto@v2.8.9)
#   peak-server-openapi     — KyuzanInc/peak (subpath), pin = git tag
#
# The script writes the new snapshot, updates SOURCES.md, and creates a
# branch sync/<name>-<short-pin> with a single commit. Operator opens
# the PR by hand.

set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "Usage: $0 <name> <pin>" >&2
  exit 2
fi

NAME="$1"
PIN="$2"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DEST="$REPO_ROOT/upstream-snapshots/$NAME"
SHORT_PIN="$(echo "$PIN" | cut -c1-12 | tr '/' '-' | tr ':' '-')"
BRANCH="sync/$NAME-$SHORT_PIN"

cd "$REPO_ROOT"

case "$NAME" in
  peak-sdk-unity)
    SRC_REPO="git@github.com:KyuzanInc/peak-sdk-unity.git"
    ;;
  turnkey-sdk-unity)
    SRC_REPO="git@github.com:KyuzanInc/turnkey-sdk-unity.git"
    ;;
  tkhq-sdk)
    SRC_REPO="git@github.com:tkhq/sdk.git"
    ;;
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
    cp "apps/peak-public-docs/docs/api-references/public-api.yaml" "$DEST/public-api.yaml"
    # Update the PIN.md
    cat > "$DEST/PIN.md" <<EOF
# peak-server OpenAPI snapshot

| Pin tag | Snapshot date | Source path |
|---|---|---|
| \`$PIN\` | $(date -u +%Y-%m-%d) | KyuzanInc/peak \`apps/peak-public-docs/docs/api-references/public-api.yaml\` |
EOF
    ;;
  tkhq-sdk)
    # Pin is something like crypto@v2.8.9 — extract package + version
    PKG_NAME="$(echo "$PIN" | cut -d@ -f1)"
    PKG_VERSION="$(echo "$PIN" | cut -d@ -f2)"
    mkdir -p "$DEST/$PKG_NAME"
    rm -rf "$DEST/$PKG_NAME"/*
    cp -R "packages/$PKG_NAME/src" "$DEST/$PKG_NAME/"
    cp "packages/$PKG_NAME/package.json" "$DEST/$PKG_NAME/" 2>/dev/null || true
    ;;
  peak-sdk-unity | turnkey-sdk-unity)
    rm -rf "$DEST"
    mkdir -p "$DEST"
    cp -R . "$DEST/"
    rm -rf "$DEST/.git"
    ;;
esac

cd "$REPO_ROOT"

git checkout -B "$BRANCH"
git add "upstream-snapshots/$NAME/"

# Update SOURCES.md with the new pin
SOURCES="$REPO_ROOT/upstream-snapshots/SOURCES.md"
echo
echo "Update upstream-snapshots/SOURCES.md to reflect the new pin manually,"
echo "then run:"
echo "  git add $SOURCES"
echo "  git commit -m 'port: resync $NAME @ $PIN'"
echo "  git push -u origin $BRANCH"
echo
echo "Done. Snapshot lives at upstream-snapshots/$NAME/"
