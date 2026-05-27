# Development

## Prerequisites

- .NET SDK 8.0.x (verify with `dotnet --version`)
- A GitHub PAT with `read:packages` scope, exported as `GITHUB_TOKEN`,
  for pulling `KyuzanInc.Turnkey.Sdk` from GitHub Packages (see below)
- Optional for Unity smoke: Unity 2021.2 LTS or newer, plus
  NuGetForUnity 4.x for consuming locally-packed `.nupkg`s

## GitHub Packages auth setup (one-time)

`KyuzanInc.Turnkey.Sdk` is published to GitHub Packages until it ships
to nuget.org. Restoring this repo therefore needs a personal access
token (classic, scope `read:packages`) — fine-grained tokens with
package read access also work.

The repo's `nuget.config` already declares the `github-kyuzan` source
itself, so use `dotnet nuget update source` (not `add source` — that
would fail with a duplicate-source error) to attach credentials. Pass
`--configfile` to point at your user-level config so the credentials
land outside the repo working tree:

```
export GITHUB_TOKEN=ghp_...   # PAT with read:packages
dotnet nuget update source github-kyuzan \
  --source https://nuget.pkg.github.com/KyuzanInc/index.json \
  --username <your-github-username> \
  --password "$GITHUB_TOKEN" \
  --store-password-in-clear-text \
  --configfile ~/.nuget/NuGet/NuGet.Config
```

If the user-level config has not declared the source yet,
`update source` errors with "Cannot find source". In that case run
`dotnet nuget add source ... --configfile ~/.nuget/NuGet/NuGet.Config`
once (the user-level config is independent of the repo's), then use
`update source` for refreshes.

Alternatively, edit `~/.nuget/NuGet/NuGet.Config` directly. Adding only
a `packageSourceCredentials` block (without re-declaring the source)
attaches credentials to the repo-declared source without duplication:

```xml
<configuration>
  <packageSources>
    <add key="github-kyuzan" value="https://nuget.pkg.github.com/KyuzanInc/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github-kyuzan>
      <add key="Username" value="<your-github-username>" />
      <add key="ClearTextPassword" value="ghp_..." />
    </github-kyuzan>
  </packageSourceCredentials>
</configuration>
```

If `dotnet restore` fails with `401 Unauthorized` against
`nuget.pkg.github.com`, the token is missing the `read:packages` scope
or has not been granted access to the `KyuzanInc/turnkey-sdk-csharp`
package.

## Build

```
dotnet restore peak-sdk-csharp.sln
dotnet build peak-sdk-csharp.sln -c Release
```

> **Bootstrap state (until M11 follow-up lands).** `packages.lock.json`
> files are not yet committed. Once they are, this command becomes
> `dotnet restore peak-sdk-csharp.sln --locked-mode` and CI re-enables
> the same flag. The csprojs already set
> `RestorePackagesWithLockFile=true`, so a successful restore writes
> the lock file; commit it and switch to `--locked-mode` in the same
> PR. See the `TODO(M11 follow-up)` markers in
> `.github/workflows/csharp-ci.yml` and `csharp-publish.yml`.

The build is deterministic (`Deterministic=true` in
`Directory.Build.props`) and `ContinuousIntegrationBuild=true` is
toggled on by the `CI` env var, so CI artifacts are reproducible.

## Test

```
dotnet test peak-sdk-csharp.sln -c Release --collect:"XPlat Code Coverage"
```

E2E tests are gated by environment variables:

```
export TURNKEY_TEST_ORG_API_KEY=...
export TURNKEY_TEST_ORG_ID=...
dotnet test peak-sdk-csharp.sln --filter "Category=E2E"
```

Without those variables, the E2E suite is silently skipped — not
failed.

## Local NuGet feed (consume packages from another project)

```
dotnet pack peak-sdk-csharp.sln -c Release -o ./local-feed
```

Point a downstream `nuget.config` at the absolute `./local-feed` path
and `dotnet add package KyuzanInc.Peak.Sdk` from there. The downstream
project also needs the GitHub Packages source configured, because
`KyuzanInc.Peak.Sdk` pulls `KyuzanInc.Turnkey.Sdk` transitively. Unity
consumers via NuGetForUnity follow the same pattern after the
`.nupkg` is copied into the Unity project's Packages folder.

## Bumping `KyuzanInc.Turnkey.Sdk`

See [docs/sync-rules.md](sync-rules.md) for the full bump procedure.
TL;DR:

1. Edit `Directory.Packages.props` to set the new version.
2. `dotnet restore peak-sdk-csharp.sln --force-evaluate`
3. Commit the refreshed `packages.lock.json` files.
4. Run the wire-format smoke locally:
   `dotnet test peak-sdk-csharp.sln --filter "FullyQualifiedName~TurnkeyWireFormatSmokeTests"`
5. Open a PR with the upstream release notes linked in the body.

## Lint / format

```
dotnet format peak-sdk-csharp.sln --verify-no-changes
```

The `.editorconfig` is normative. Roslyn analyzer severities flag
weak / broken cryptographic algorithm usage as errors.

## Re-sync upstream snapshots

```
./scripts/sync-upstream.sh peak-sdk-unity <new-commit-sha>
./scripts/sync-upstream.sh peak-server-openapi <new-tag>
```

Each command produces one commit on a `sync/` branch. Review and
merge per [docs/sync-rules.md](sync-rules.md).

## Cross-platform notes

- Windows: DPAPI is the default `ISecureStorage`. Other platforms
  return `ISecureStorage.IsAvailable == false`.
- macOS / Linux: build and test green on .NET 8; no built-in
  secure storage in v0.1.0.
- Unity standalone: respects the host OS rule above; mobile gets the
  Unity adapter implementations.
