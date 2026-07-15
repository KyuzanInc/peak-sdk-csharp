# peak-sdk-csharp

.NET (NuGet) SDK for the Peak embedded-wallet platform. A
community-maintained port of the Unity-only `peak-sdk-unity` package,
generalised so that Godot, console apps, .NET MAUI, and any modern
.NET host can use the same code path Unity does today.

**Status: pre-release (v0.1.0-alpha).** The `KyuzanInc.Peak.Sdk` package
publishes to GitHub Packages on a `v*` tag (`v0.1.0-alpha.1` is the
current release); a public nuget.org publish is scheduled for a later
milestone. Both `KyuzanInc.Peak.Sdk` and its Turnkey crypto dependency
ship from GitHub Packages, so consumers need GitHub Packages auth set up
locally (see [docs/development.md](docs/development.md)).

## Packages

This is a multi-project repo. Only `KyuzanInc.Peak.Sdk` is packable and
published; the generated client project is internal build-time tooling.

| Package | TFM | Purpose |
|---|---|---|
| `KyuzanInc.Peak.Sdk` | `netstandard2.1;net8.0;net8.0-windows` | The Peak SDK itself. Adds `PeakClient`, OTP login, account/private-key services, `IStorage`/`ISecureStorage` abstractions, Windows DPAPI secure storage. |
| `KyuzanInc.Peak.PublicApiClient` | `netstandard2.1;net8.0` | Auto-generated OpenAPI client. Build-time only: backs spec-drift CI and the DTO field-coverage contract test; **not** referenced by the SDK at runtime or shipped in its package. |

`KyuzanInc.Peak.Sdk` is the only NuGet artifact this repository currently
publishes. `KyuzanInc.Peak.PublicApiClient` is an internal, non-packable
build-time project. In particular, there is no shipped
`KyuzanInc.Peak.Sdk.Unity` package and no C# `KeychainSecureStorage` or
`KeyStoreSecureStorage` `ISecureStorage` class.

Unity consumers use the separate
[`com.kyuzan.peak-sdk-unity`](https://github.com/KyuzanInc/peak-sdk-unity)
UPM package, which consumes `KyuzanInc.Peak.Sdk`. Its upcoming v0.8.0 release
is planned to keep `EncryptedPlayerPrefsStorage` as an explicit opt-in that
implements the core `IStorage` contract while obtaining its data-encryption key
from iOS Keychain or Android Keystore on mobile players. The planned storage
path does not request Face ID, Touch ID, Android biometrics, or a device
passcode. The default will remain volatile `InMemoryStorage`; Unity Editor and
desktop players will retain the software-derived interim provider for
development only. Until v0.8.0 is released, treat this as release-candidate
behavior rather than an available package guarantee. See the
[storage threat model](docs/security/storage-threat-model.md) and the Unity package's
[session-management documentation](https://github.com/KyuzanInc/peak-sdk-unity/blob/main/README.md#session-management).

The copy under `upstream-snapshots/peak-sdk-unity/` is a read-only port
reference, not the Unity package implementation used by consumers.

## Dependencies

| Dependency | Source | Pin |
|---|---|---|
| `KyuzanInc.Turnkey.Sdk` | [KyuzanInc/turnkey-sdk-csharp](https://github.com/KyuzanInc/turnkey-sdk-csharp) on GitHub Packages | `[0.1.0-alpha.0]` (exact, via CPM + committed `packages.lock.json`) |

The Turnkey port (Crypto, ApiKeyStamper, Http, Encoding) lives in a
separate repo and ships as a NuGet package. Crypto changes happen
there, are reviewed by multi-round Codex in that repo, and reach this
SDK as a deliberate version bump (see
[docs/sync-rules.md](docs/sync-rules.md)).

## What this repo is *not*

- Not on `nuget.org` yet — the released `KyuzanInc.Peak.Sdk` package ships
  from GitHub Packages. The nuget.org publish is wired but gated on a
  separate workstream.
- Not the source of the Turnkey crypto port. Crypto code is in
  [`KyuzanInc/turnkey-sdk-csharp`](https://github.com/KyuzanInc/turnkey-sdk-csharp);
  see that repo for the audit status and review evidence.
- Not a 1:1 mirror of `peak-sdk-browser`. We port the Unity public surface
  first and add browser-only flows (Google OAuth, IndexedDB session) in
  later milestones.

## Quick start

For consumers (with GitHub Packages auth set up):

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

> **Response validation.** The SDK returns server responses as nullable DTOs and
> does not enforce field presence: a missing field deserializes to its default
> (`null` / `0`), not an error. Before you display or transact on identity and
> address fields — notably `AccountResponse.Id` and `AccountAddressResponse.Address`
> — null/empty-check them. A silently empty/defaulted address is a fund-loss-shaped
> failure if trusted blindly. (A future `[JsonRequired]` hardening pass is the
> recommended fix if you want the SDK itself to fail closed — spec §6.3 / R6.)

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
│   └── peak-public-api-client-csharp/  KyuzanInc.Peak.PublicApiClient
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
