# Upstream sources — pin map

Read-only mirrors of the source code from which `peak-sdk-csharp` is
ported. Resync is operator-driven (`scripts/sync-upstream.sh`); each
resync produces one commit per source so the diff is reviewable.

The mirrors are **never** edited in place. Code lives under
`packages/<name>-csharp/src/`; the mirror is the reference, not the
build input.

## peak-sdk-unity

| Field | Value |
|---|---|
| Source repo | https://github.com/KyuzanInc/peak-sdk-unity |
| Pinned commit | `fc560e8b18bcc28b6a863520e82115513d6c8ced` |
| Snapshot date | 2026-05-27 |
| Snapshot path | `upstream-snapshots/peak-sdk-unity/` |
| Purpose | Source of the Unity-shaped Peak SDK API (PeakSdk, AuthenticatedPeakSdk, services, models, exceptions) |

## turnkey-sdk-unity

| Field | Value |
|---|---|
| Source repo | https://github.com/KyuzanInc/turnkey-sdk-unity |
| Pinned commit | `039d8e4801095e46cbadca188702535a0e76e5dd` |
| Snapshot date | 2026-05-27 |
| Snapshot path | `upstream-snapshots/turnkey-sdk-unity/` |
| Purpose | Source of `Crypto.cs` (port of `@turnkey/crypto@2.8.9`), `ApiKeyStamper.cs` (port of `@turnkey/api-key-stamper@0.6.0`), `Http.cs` (port of `@turnkey/http@3.16.0`), `Encoding.cs`, `UnityConstants.cs` |

## turnkey-official-src (deferred)

The Turnkey TypeScript libraries that `turnkey-sdk-unity` itself was
ported from. Used by `codex-crypto-reviews/codex-crypto-review.sh` for
the 1:1 logical equivalence checks (D17-CLAR).

| Field | Value |
|---|---|
| Source repo | https://github.com/tkhq/sdk |
| `@turnkey/crypto` | v2.8.9 |
| `@turnkey/api-key-stamper` | v0.6.0 |
| `@turnkey/http` | v3.16.0 |
| Snapshot path | `upstream-snapshots/turnkey-official-src/` |
| Status | Deferred to first Codex review run; fetched by `scripts/sync-upstream.sh tkhq-sdk` |

## peak-server-openapi (deferred until PR 2)

| Field | Value |
|---|---|
| Source repo | https://github.com/KyuzanInc/peak (path: `apps/peak-public-docs/docs/api-references/public-api.yaml`) |
| Pinned tag | TBD — OQ-N1, Komy CTO to pick before PR 2 |
| Snapshot path | `upstream-snapshots/peak-server-openapi/` |
| Status | Deferred to PR 2 start |
