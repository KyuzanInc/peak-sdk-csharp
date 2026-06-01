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
| We want peak-server's latest public API (its `main` moved) | `upstream-snapshots/peak-server-openapi/public-api.yaml` | resync from `main` + regenerate the OpenAPI client |
| Consumer reports a behaviour mismatch with `peak-sdk-browser` | depends on root cause | usually no resync; instead update the C# DTO wrapper |

## Workflow

### Upstream snapshot resync

```
./scripts/sync-upstream.sh <name> <pin>
```

Where `<name>` is one of `peak-sdk-unity` or `peak-server-openapi`,
and `<pin>` is the new commit SHA, tag, or branch. For
`peak-server-openapi` we track `main` — pass `main` and the script records
the resolved HEAD commit in `PIN.md`.

The script:

1. Fetches the new source.
2. Replaces the corresponding `upstream-snapshots/<name>/` directory (and,
   for `peak-server-openapi`, writes its `PIN.md` with the resolved HEAD
   commit).
3. Creates a branch `sync/<name>-<short-pin>` and stages the snapshot.
4. Prints the manual follow-up: update `upstream-snapshots/SOURCES.md`, then
   `git add` + `git commit` + `git push` (the script does not commit for you),
   and regenerate downstream artefacts (e.g. the OpenAPI client).

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

- Re-generate the client with `scripts/generate-public-api-client.sh`
  (engine: `@openapitools/openapi-generator-cli`, core pinned to 7.9.0 by
  `packages/peak-public-api-client-csharp/openapitools.json`; settings in
  `packages/peak-public-api-client-csharp/openapi-config.yaml`).
- Build the client and commit the regenerated
  `packages/peak-public-api-client-csharp/src/` plus the refreshed
  `packages.lock.json`.
- Drift CI compares the regenerated artefacts byte-for-byte with the
  committed `src/`; a mismatch fails the build.
- If/when the client is wired into `KyuzanInc.Peak.Sdk`, update the DTO
  wrappers in `packages/peak-sdk-csharp/src/Models/` if the public surface
  gained or lost fields.

## Drift CI

`.github/workflows/csharp-ci.yml` runs an `openapi-client-drift` job that:

- Regenerates the C# client from the committed
  `upstream-snapshots/peak-server-openapi/public-api.yaml` using
  `scripts/generate-public-api-client.sh`.
- Fails the build if the committed
  `packages/peak-public-api-client-csharp/src/` differs from the
  regenerated output, naming the drift in the log.

The job needs a JRE + Node (the generator is Java, launched via `npx`). It
does **not** fetch peak; comparing the snapshot against the live
`peak-server` `main` HEAD is a manual operator step via
`scripts/sync-upstream.sh peak-server-openapi main`.
