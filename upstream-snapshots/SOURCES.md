# Upstream sources — pin map

This repository retains one reviewed OpenAPI contract as a reproducible input.
It does not retain a complete copy of any upstream source repository.
Resynchronization is operator-driven with `scripts/sync-upstream.sh`; each
change is reviewed in a dedicated pull request.

## peak-server OpenAPI

| Field | Value |
|---|---|
| Source repo | https://github.com/KyuzanInc/peak (path: `apps/peak-public-docs/docs/api-references/public-api.yaml`) |
| Tracks | `main` (latest commit, not a release tag) |
| Last synced commit | `72ca08b3eade7117334ad481f6a1b29a6bee73a9` |
| Imported contract SHA-256 | `fe6f4d6a7c8d9fc93d0b36a754059b0722031a5fe33af3164fdcca0ff93c0596` |
| Snapshot date | 2026-05-30 |
| Public contract path | `upstream-snapshots/peak-server-openapi/public-api.yaml` |
| Purpose | Source spec for the internal, build-time-only `KyuzanInc.Peak.PublicApiClient` code generator |

The committed contract is sanitized for public distribution after import. Its
public checksum is recorded in
`tests/UpstreamSources/peak-server-openapi-public-api.sha256`.

Historical Unity provenance is recorded in
[`docs/compatibility/upstream-pins.md`](../docs/compatibility/upstream-pins.md).
The Unity source tree is intentionally not retained; `peak-sdk-unity` is a
separate downstream adapter.
