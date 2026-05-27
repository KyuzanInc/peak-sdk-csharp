# Development

## Prerequisites

- .NET SDK 8.0.x (verify with `dotnet --version`)
- Optional for Unity smoke: Unity 2021.2 LTS or newer, plus
  NuGetForUnity 4.x for consuming locally-packed `.nupkg`s
- Optional for crypto review: Codex CLI (`npm install -g @openai/codex`)
  and a valid OpenAI session

## Build

```
dotnet restore peak-sdk-csharp.sln
dotnet build peak-sdk-csharp.sln -c Release
```

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
and `dotnet add package KyuzanInc.Peak.Sdk` from there. Unity
consumers via NuGetForUnity follow the same pattern after the
`.nupkg` is copied into the Unity project's Packages folder.

## Lint / format

```
dotnet format peak-sdk-csharp.sln --verify-no-changes
```

The `.editorconfig` is normative. Roslyn analyzer severities flag
weak / broken cryptographic algorithm usage as errors.

## Crypto multi-round review (when touching `turnkey-sdk-csharp/src/`)

```
./codex-crypto-reviews/codex-crypto-review.sh Crypto.cs 1
./codex-crypto-reviews/codex-crypto-review.sh Crypto.cs 2
./codex-crypto-reviews/codex-crypto-review.sh Crypto.cs 3
```

Each round saves evidence to
`codex-crypto-reviews/Crypto.cs-r<N>-<date>.md`. Three consecutive
"No logic divergence found" rounds are required before a crypto
change can merge. See
[docs/security/crypto-port-policy.md](security/crypto-port-policy.md).

## Re-sync upstream snapshots

```
./scripts/sync-upstream.sh peak-sdk-unity <new-commit-sha>
./scripts/sync-upstream.sh turnkey-sdk-unity <new-commit-sha>
./scripts/sync-upstream.sh tkhq-sdk <new-version>
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
