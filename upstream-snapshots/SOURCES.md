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

## peak-server-openapi (deferred until PR 2)

| Field | Value |
|---|---|
| Source repo | https://github.com/KyuzanInc/peak (path: `apps/peak-public-docs/docs/api-references/public-api.yaml`) |
| Pinned tag | TBD — OQ-N1, Komy CTO to pick before PR 2 |
| Snapshot path | `upstream-snapshots/peak-server-openapi/` |
| Status | Deferred to PR 2 start |
