# peak-sdk-csharp

.NET (NuGet) SDK for the Peak embedded-wallet platform. A
community-maintained port of the Unity-only `peak-sdk-unity` package,
generalised so that Godot, console apps, .NET MAUI, and any modern
.NET host can use the same code path Unity does today.

**Status: pre-release (v0.1.0-alpha).** Public NuGet publish is scheduled
for a later milestone; until then every package is consumed via local
`.nupkg` feeds or by a project reference. The Turnkey crypto dependency
ships from GitHub Packages — consumers need GitHub Packages auth set up
locally (see [docs/development.md](docs/development.md)).

## Packages

This is a multi-package repo. Each package builds as its own NuGet artifact.

| Package | TFM | Purpose |
|---|---|---|
| `KyuzanInc.Peak.Sdk` | `netstandard2.1;net8.0;net8.0-windows` | The Peak SDK itself. Adds `PeakClient`, OTP login, account/private-key services, `IStorage`/`ISecureStorage` abstractions, Windows DPAPI secure storage. |
| `KyuzanInc.Peak.PublicApiClient` | `netstandard2.1;net8.0` | Auto-generated OpenAPI client. `internal` consumers only. Re-exposed by the SDK behind hand-designed DTOs. |
| `KyuzanInc.Peak.Sdk.Unity` | `netstandard2.1` | Unity-only platform adapter: PlayerPrefs storage (opt-in only, plaintext warning), iOS Keychain via P/Invoke, Android KeyStore via JNI. |

The `peak-sdk-unity` repo will become a thin Unity adapter on top of
`KyuzanInc.Peak.Sdk.Unity` in a later release; the source code lives at
`upstream-snapshots/peak-sdk-unity/` for now (read-only, port reference).

## Dependencies

| Dependency | Source | Pin |
|---|---|---|
| `KyuzanInc.Turnkey.Sdk` | [KyuzanInc/turnkey-sdk-csharp](https://github.com/KyuzanInc/turnkey-sdk-csharp) on GitHub Packages | `[0.1.0-alpha.0]` (exact, via CPM + lock files) |

The Turnkey port (Crypto, ApiKeyStamper, Http, Encoding) lives in a
separate repo and ships as a NuGet package. Crypto changes happen
there, are reviewed by multi-round Codex in that repo, and reach this
SDK as a deliberate version bump (see
[docs/sync-rules.md](docs/sync-rules.md)).

## What this repo is *not*

- Not on `nuget.org` yet. Public publish is gated on a separate workstream.
- Not the source of the Turnkey crypto port. Crypto code is in
  [`KyuzanInc/turnkey-sdk-csharp`](https://github.com/KyuzanInc/turnkey-sdk-csharp);
  see that repo for the audit status and review evidence.
- Not a 1:1 mirror of `peak-sdk-browser`. We port the Unity public surface
  first and add browser-only flows (Google OAuth, IndexedDB session) in
  later milestones.

## Quick start

For consumers (after the SDK is published and GitHub Packages auth is set up):

```csharp
using KyuzanInc.Peak.Sdk;

var client = PeakClient.Initialize(new PeakClientOptions
{
    ProjectApiKey = "...",
    ApiUrl = "https://api.peak.xyz",
});

var init = await client.InitOtpLoginAsync("user@example.com");
var login = await client.CompleteOtpLoginAsync("user@example.com", init.OtpId, "123456");

var authClient = client.Authenticate();
var accounts = await authClient.ListAccountsAsync();
```

For maintainers, see [docs/development.md](docs/development.md) for the
build/test workflow and [plans/plans-peak-sdk-csharp.md](plans/plans-peak-sdk-csharp.md)
for the active port plan.

## Repository layout

```
peak-sdk-csharp/
├── peak-sdk-csharp.sln              Root solution (all packages + tests)
├── Directory.Build.props            Shared MSBuild props (LangVersion, Nullable, etc.)
├── Directory.Packages.props         Central package management (CPM) pins
├── nuget.config                     packageSourceMapping for nuget.org + GitHub Packages + local feed
├── packages/
│   ├── peak-sdk-csharp/             KyuzanInc.Peak.Sdk
│   ├── peak-public-api-client-csharp/  KyuzanInc.Peak.PublicApiClient
│   └── peak-sdk-csharp-unity/       KyuzanInc.Peak.Sdk.Unity (Unity adapter)
├── examples/                        Godot + console smoke
├── upstream-snapshots/              Read-only copies of port sources:
│   ├── peak-sdk-unity/              Pinned @ commit SHA in upstream-snapshots/SOURCES.md
│   └── peak-server-openapi/         peak-server OpenAPI spec @ tag
├── docs/
│   ├── architecture.md
│   ├── development.md
│   ├── security/
│   │   ├── storage-threat-model.md
│   │   └── secure-storage-platform-matrix.md
│   └── sync-rules.md                OpenAPI + Unity-source sync workflow
├── plans/
│   └── plans-peak-sdk-csharp.md     Active port plan (decisions, status, adjudication)
└── .github/workflows/
    ├── csharp-ci.yml                Build + test matrix
    └── csharp-publish.yml           NuGet publish
```

## License

[MIT](LICENSE). Turnkey crypto primitives are consumed from the
external `KyuzanInc.Turnkey.Sdk` package and licensed under its
original Turnkey terms; see that package for notices.
