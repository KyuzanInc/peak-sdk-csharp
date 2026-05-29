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

## peak-server-openapi

| Field | Value |
|---|---|
| Source repo | https://github.com/KyuzanInc/peak (path: `apps/peak-public-docs/docs/api-references/public-api.yaml`) |
| Pinned tag | `v0.3.0` (provisional — OQ-N1, Komy CTO to confirm before v0.1.0) |
| Snapshot date | 2026-05-29 |
| Snapshot path | `upstream-snapshots/peak-server-openapi/` |
| Purpose | Source spec for the internal `KyuzanInc.Peak.PublicApiClient` OpenAPI codegen (see `docs/sync-rules.md`) |
