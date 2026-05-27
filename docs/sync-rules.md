# Sync rules — upstream-snapshots, OpenAPI, and the Turnkey SDK pin

`peak-sdk-csharp` mirrors several external sources into
`upstream-snapshots/` and an OpenAPI spec into
`upstream-snapshots/peak-server-openapi/`. The mirrors are read-only
port references; the build never compiles them directly. The Turnkey
crypto layer is consumed as the external
[`KyuzanInc.Turnkey.Sdk`](https://github.com/KyuzanInc/turnkey-sdk-csharp)
NuGet package pinned in `Directory.Packages.props`. This document
describes when each pin / mirror must be refreshed and how to do it
safely.

## When to re-sync

| Trigger | Affected pin / mirror | Operator action |
|---|---|---|
| `KyuzanInc/peak-sdk-unity` ships a relevant change | `upstream-snapshots/peak-sdk-unity/` | resync + port if the change is in `Runtime/` |
| `KyuzanInc/turnkey-sdk-csharp` cuts a new release | `Directory.Packages.props` pin + lock files | bump per "Bump KyuzanInc.Turnkey.Sdk" below |
| `peak-server` cuts a release we want to support | `upstream-snapshots/peak-server-openapi/public-api.yaml` | resync + regenerate the OpenAPI client |
| Consumer reports a behaviour mismatch with `peak-sdk-browser` | depends on root cause | usually no resync; instead update the C# DTO wrapper |

## Workflow

### Upstream snapshot resync

```
./scripts/sync-upstream.sh <name> <pin>
```

Where `<name>` is one of `peak-sdk-unity` or `peak-server-openapi`,
and `<pin>` is the new commit SHA or tag.

The script:

1. Fetches the new source.
2. Replaces the corresponding `upstream-snapshots/<name>/` directory.
3. Updates `upstream-snapshots/SOURCES.md`.
4. Emits a single commit on a branch `sync/<name>-<short-pin>`.
5. Prints the next required action (port, regenerate, or both).

### Bump KyuzanInc.Turnkey.Sdk

1. Confirm the new release on
   <https://github.com/KyuzanInc/turnkey-sdk-csharp/releases>. Read the
   release notes — crypto changes need extra care.
2. Edit `Directory.Packages.props` and update the
   `<PackageVersion Include="KyuzanInc.Turnkey.Sdk" Version="[<new>]" />`
   row (keep the square brackets — the pin must stay exact).
3. Re-resolve the lock files:
   ```
   dotnet restore peak-sdk-csharp.sln --force-evaluate
   ```
4. Inspect the diff on
   `packages/peak-sdk-csharp/src/packages.lock.json` and
   `packages/peak-sdk-csharp/tests/packages.lock.json`. Commit both
   alongside the props change.
5. Run the wire-format smoke against the new package:
   ```
   dotnet test peak-sdk-csharp.sln \
     --filter "FullyQualifiedName~TurnkeyWireFormatSmokeTests"
   ```
   A failure means the external surface drifted in a way the
   consumer-side code is not yet aligned with.
6. Open a PR with title prefix `port:` and link the upstream release
   notes in the body.

## Post-sync mandatory steps

### `peak-sdk-unity` resync

- Re-run `git diff` against the old mirror to surface the change.
- Apply the equivalent edit to `packages/peak-sdk-csharp/src/`.
- Refresh tests if the change touches a tested code path.

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
