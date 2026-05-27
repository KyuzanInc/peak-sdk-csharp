#!/usr/bin/env bash
# Codex multi-round crypto review runner.
#
# Usage:
#   codex-crypto-reviews/codex-crypto-review.sh <file.cs> [round_number]
#
# Examples:
#   codex-crypto-reviews/codex-crypto-review.sh Crypto.cs 1
#   codex-crypto-reviews/codex-crypto-review.sh ApiKeyStamper.cs 2
#
# The script:
#   1. Resolves the corresponding TypeScript source from
#      upstream-snapshots/turnkey-official-src/ via turnkey-source-pins.md.
#   2. Calls `codex exec` with the standard review prompt template.
#   3. Saves the response to codex-crypto-reviews/<file>-r<N>-<date>.md.
#
# Per docs/security/crypto-port-policy.md, each crypto file needs >= 3
# consecutive rounds reporting "no logic divergence" before merge.

set -euo pipefail

if [ "$#" -lt 1 ]; then
  echo "Usage: $0 <file.cs> [round_number]" >&2
  exit 2
fi

FILE="$1"
ROUND="${2:-1}"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC_FILE="$REPO_ROOT/packages/turnkey-sdk-csharp/src/$FILE"
DATE="$(date +%Y-%m-%d)"
OUT="$REPO_ROOT/codex-crypto-reviews/${FILE}-r${ROUND}-${DATE}.md"

if [ ! -f "$SRC_FILE" ]; then
  echo "ERROR: $SRC_FILE not found" >&2
  exit 1
fi

PROMPT="IMPORTANT: Do NOT read or execute any files under ~/.claude/, ~/.agents/, .claude/skills/, or agents/. Stay focused on this repository.

You are reviewing a C# port of Turnkey official source code (@turnkey/{crypto,api-key-stamper,http}). The pinned official versions are in codex-crypto-reviews/turnkey-source-pins.md and the Unity port-base commits are in codex-crypto-reviews/unity-source-pins.md.

File under review: packages/turnkey-sdk-csharp/src/${FILE}

Compare the C# file with:
  1. The corresponding TypeScript source (read from upstream-snapshots/turnkey-official-src/ if present, or fetch from the Turnkey github at the pinned version).
  2. The Unity port-base C# (read from upstream-snapshots/turnkey-sdk-unity/Runtime/${FILE}) — this is the file we are porting FROM. Use it to spot Unity-specific code that should have been removed (JsonUtility.ToJson, UnityEngine usings, etc.) and Unity-specific code that was correctly preserved (Newtonsoft -> System.Text.Json conversions, etc.).

Identify every place where the C# logic deviates from the TypeScript upstream (different algorithm step, different constant, different error handling, different rounding/normalization).

Distinguish:
  (a) intentional .NET adaptation (Task<T> vs Promise, byte[] vs Uint8Array, BigInteger vs bigint, JsonSerializer vs JSON.parse, etc.) — OK, do not list.
  (b) actual logic divergence — NOT OK, list ALL of these.

Output structure (Japanese):

## Round ${ROUND} — ${FILE}

### Category (b) divergence findings

For each finding:
- C# location: src/${FILE}:<line>
- TS upstream: <file>:<line>
- Divergence: <one paragraph>
- Severity: critical | high | low

If no category (b) findings: write 'No logic divergence found.'

### Notes (optional)
Category (a) entries worth flagging (e.g. unusual but correct adaptations)."

echo "Running Codex round ${ROUND} for ${FILE}..."
echo "Output will land at: ${OUT}"

# Codex CLI 0.130+ rejects --enable web_search_cached; rely on default web_search config.
gtimeout 600 codex exec "$PROMPT" \
  -C "$REPO_ROOT" \
  -s read-only \
  -c 'model_reasoning_effort="high"' \
  --json < /dev/null | python3 -c "
import sys, json
out_lines = []
for line in sys.stdin:
    line = line.strip()
    if not line: continue
    try:
        obj = json.loads(line)
        t = obj.get('type','')
        if t == 'item.completed' and 'item' in obj:
            item = obj['item']
            itype = item.get('type','')
            text = item.get('text','')
            if itype == 'agent_message' and text:
                out_lines.append(text)
        elif t == 'turn.completed':
            usage = obj.get('usage',{})
            tokens = usage.get('input_tokens',0) + usage.get('output_tokens',0)
            if tokens:
                out_lines.append(f'\\n---\\ntokens: {tokens}\\n')
    except: pass
print(''.join(out_lines))
" > "$OUT"

echo "Saved to: ${OUT}"
echo
echo "Next steps:"
echo "  1. Read ${OUT}"
echo "  2. Address any category (b) findings in src/${FILE}"
echo "  3. Re-run with ROUND=$((ROUND + 1)) once changes are in"
echo "  4. Three consecutive 'No logic divergence found' rounds = ready to merge"
