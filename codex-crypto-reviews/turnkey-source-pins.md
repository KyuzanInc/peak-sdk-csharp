# Turnkey official source pins (1:1 port reference)

The pinned versions of Turnkey's TypeScript SDK that
`packages/turnkey-sdk-csharp/src/` is logically equivalent to. Codex
multi-round review uses these as the comparison target.

| TypeScript package | Pin | github.com/tkhq/sdk path |
|---|---|---|
| `@turnkey/crypto` | v2.8.9 | `packages/crypto/src/*` |
| `@turnkey/api-key-stamper` | v0.6.0 | `packages/api-key-stamper/src/index.ts` |
| `@turnkey/http` | v3.16.0 | `packages/http/src/*` (stamping methods only) |
| `@turnkey/encoding` | v0.6.0 | `packages/encoding/src/*` |

Resolved commit SHAs (TBD on first sync run):

| Pin | Commit SHA |
|---|---|
| `@turnkey/crypto@2.8.9` | _to be filled by `scripts/sync-upstream.sh tkhq-sdk`_ |
| `@turnkey/api-key-stamper@0.6.0` | _to be filled_ |
| `@turnkey/http@3.16.0` | _to be filled_ |
| `@turnkey/encoding@0.6.0` | _to be filled_ |

## C# → TypeScript file map

| C# file | TypeScript file(s) |
|---|---|
| `src/Encoding.cs` | `@turnkey/encoding/src/index.ts` |
| `src/Crypto.cs` (constants) | `@turnkey/crypto/src/constants.ts` |
| `src/Crypto.cs` (ModSqrt) | `@turnkey/crypto/src/math.ts` |
| `src/Crypto.cs` (HKDF) | `@turnkey/crypto/src/hkdf.ts` (or equivalent `@noble/hashes/hkdf` shape) |
| `src/Crypto.cs` (HPKE, key derivation, bundle parse) | `@turnkey/crypto/src/turnkey.ts` + `@turnkey/crypto/src/index.ts` |
| `src/Crypto.cs` (JWT verify) | `@turnkey/crypto/src/jwt.ts` |
| `src/ApiKeyStamper.cs` | `@turnkey/api-key-stamper/src/index.ts` |
| `src/Http.cs` | `@turnkey/http/src/genericClient.ts` + per-activity types |

When the upstream version is bumped:

1. Update the table above.
2. Re-run `codex-crypto-reviews/codex-crypto-review.sh` for **every**
   affected file.
3. Commit fresh evidence files; do not overwrite history.
