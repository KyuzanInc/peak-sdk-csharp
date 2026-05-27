# Sync rules — upstream-snapshots and OpenAPI

`peak-sdk-csharp` mirrors several external sources into
`upstream-snapshots/` and an OpenAPI spec into
`upstream-snapshots/peak-server-openapi/`. The mirrors are read-only
port references; the build never compiles them directly. This document
describes when each mirror must be refreshed and how to do it
safely.

## When to re-sync

| Trigger | Affected mirror | Operator action |
|---|---|---|
| `KyuzanInc/peak-sdk-unity` ships a relevant change | `upstream-snapshots/peak-sdk-unity/` | resync + port if the change is in `Runtime/` |
| `KyuzanInc/turnkey-sdk-unity` ships a relevant change | `upstream-snapshots/turnkey-sdk-unity/` | resync + port if the change is in `Runtime/` |
| `tkhq/sdk` ships `@turnkey/{crypto,api-key-stamper,http}` major / minor | `upstream-snapshots/turnkey-official-src/` | resync + re-run Codex multi-round review on the affected C# file |
| `peak-server` cuts a release we want to support | `upstream-snapshots/peak-server-openapi/public-api.yaml` | resync + regenerate the OpenAPI client |
| Consumer reports a behaviour mismatch with `peak-sdk-browser` | depends on root cause | usually no resync; instead update the C# DTO wrapper |

## Workflow

```
./scripts/sync-upstream.sh <name> <pin>
```

Where `<name>` is one of `peak-sdk-unity`, `turnkey-sdk-unity`,
`tkhq-sdk`, or `peak-server-openapi`, and `<pin>` is the new commit
SHA, tag, or version.

The script:

1. Fetches the new source.
2. Replaces the corresponding `upstream-snapshots/<name>/` directory.
3. Updates `upstream-snapshots/SOURCES.md` and (for crypto) the pins
   in `codex-crypto-reviews/turnkey-source-pins.md`.
4. Emits a single commit on a branch `sync/<name>-<short-pin>`.
5. Prints the next required action (port, regenerate, or both).

## Post-sync mandatory steps

### `peak-sdk-unity` / `turnkey-sdk-unity` resync

- Re-run `git diff` against the old mirror to surface the change.
- Apply the equivalent edit to `packages/<name>-csharp/src/`.
- Refresh tests if the change touches a tested code path.

### `tkhq-sdk` resync (crypto-critical)

- Re-pin in `codex-crypto-reviews/turnkey-source-pins.md`.
- Re-run `codex-crypto-reviews/codex-crypto-review.sh` for **every**
  affected file. Old evidence is preserved for history; the new
  evidence carries the new date.
- Add upstream-supplied test vectors to
  `packages/turnkey-sdk-csharp/tests/Fixtures/` if the change touches
  HPKE, HKDF, ECDSA, or bundle parsing.
- The PR title prefix is `port:`.

### `peak-server-openapi` resync

- Re-generate `packages/peak-public-api-client-csharp/src/` with
  `pnpm openapi-generator-cli generate` (or the project-local
  equivalent — codegen settings live in
  `packages/peak-public-api-client-csharp/openapi-config.yaml`).
- Drift CI compares the regenerated artefacts byte-for-byte with the
  committed `src/`; a mismatch fails the build.
- Update the DTO wrappers in `packages/peak-sdk-csharp/src/Models/`
  if the public surface gained or lost fields.

## Drift CI

`.github/workflows/csharp-ci.yml` runs a `drift-check` job that:

- Diffs `upstream-snapshots/peak-server-openapi/public-api.yaml`
  against the live spec at the pinned `peak-server` tag.
- Diffs the generated `peak-public-api-client-csharp/src/` against
  what the codegen produces today.
- Fails the build with a clear message naming the file that drifted.

Drift CI is informative only; merging a drift-failing PR requires an
explicit "resync intended" label.
