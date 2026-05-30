#!/usr/bin/env bash
# sync-upstream.sh — re-snapshot one upstream source into upstream-snapshots/
#
# Usage:
#   scripts/sync-upstream.sh <name> <pin>
#
# Where <name> is one of:
#   peak-sdk-unity          — KyuzanInc/peak-sdk-unity, pin = commit SHA
#   peak-server-openapi     — KyuzanInc/peak (subpath), pin = main (tracks
#                             the branch; records the resolved HEAD commit)
#
# The script writes the new snapshot, updates SOURCES.md, and creates a
# branch sync/<name>-<short-pin> with a single commit. Operator opens
# the PR by hand.
#
# Turnkey crypto (Crypto / ApiKeyStamper / Http / Encoding) lives in
# KyuzanInc/turnkey-sdk-csharp and is consumed as the
# KyuzanInc.Turnkey.Sdk NuGet package; resyncing its sources is done
# from that repo, not this one. See docs/sync-rules.md.

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
    mkdir -p "$DEST"
    cp "apps/peak-public-docs/docs/api-references/public-api.yaml" "$DEST/public-api.yaml"
    # Update the PIN.md
    cat > "$DEST/PIN.md" <<EOF
# peak-server OpenAPI snapshot

| Pin tag | Snapshot date | Source path |
|---|---|---|
| \`$PIN\` | $(date -u +%Y-%m-%d) | KyuzanInc/peak \`apps/peak-public-docs/docs/api-references/public-api.yaml\` |
EOF
    ;;
  peak-sdk-unity)
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
