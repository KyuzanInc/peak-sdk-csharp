# peak-sdk-csharp

.NET (NuGet) SDK for the Peak embedded-wallet platform and its Turnkey
key-management primitives. A community-maintained port of the Unity-only
`peak-sdk-unity` + `turnkey-sdk-unity` packages, generalised so that Godot,
console apps, .NET MAUI, and any modern .NET host can use the same code
path Unity does today.

**Status: pre-release (v0.1.0-alpha).** Public NuGet publish is scheduled
for a later milestone; until then every package is consumed via local
`.nupkg` feeds or by a project reference.

## Packages

This is a multi-package repo. Each package builds as its own NuGet artifact.

| Package | TFM | Purpose |
|---|---|---|
| `KyuzanInc.Turnkey.Sdk` | `netstandard2.1;net8.0` | Crypto primitives (P-256 ECDSA, HPKE, HKDF, API key stamping) ported from `@turnkey/{crypto,api-key-stamper,http}`. Unofficial; not affiliated with Turnkey, Inc. |
| `KyuzanInc.Peak.Sdk` | `netstandard2.1;net8.0;net8.0-windows` | The Peak SDK itself. Adds `PeakClient`, OTP login, account/private-key services, `IStorage`/`ISecureStorage` abstractions, Windows DPAPI secure storage. |
| `KyuzanInc.Peak.PublicApiClient` | `netstandard2.1;net8.0` | Auto-generated OpenAPI client. `internal` consumers only. Re-exposed by the SDK behind hand-designed DTOs. |
| `KyuzanInc.Peak.Sdk.Unity` | `netstandard2.1` | Unity-only platform adapter: PlayerPrefs storage (opt-in only, plaintext warning), iOS Keychain via P/Invoke, Android KeyStore via JNI. |

The `peak-sdk-unity` repo will become a thin Unity adapter on top of
`KyuzanInc.Peak.Sdk.Unity` in a later release; the source code lives at
`upstream-snapshots/peak-sdk-unity/` for now (read-only, port reference).

## What this repo is *not*

- Not on `nuget.org` yet. Public publish is gated on a separate workstream.
- Not a fork of Turnkey's official TypeScript SDK. The crypto port is
  byte-compatible with `@turnkey/crypto@2.8.9` and verified by multi-round
  Codex review; it has not undergone a paid third-party security audit.
- Not a 1:1 mirror of `peak-sdk-browser`. We port the Unity public surface
  first and add browser-only flows (Google OAuth, IndexedDB session) in
  later milestones.

## Quick start

For consumers (after the SDK is published):

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
├── packages/
│   ├── turnkey-sdk-csharp/          KyuzanInc.Turnkey.Sdk
│   ├── peak-sdk-csharp/             KyuzanInc.Peak.Sdk
│   ├── peak-public-api-client-csharp/  KyuzanInc.Peak.PublicApiClient
│   └── peak-sdk-csharp-unity/       KyuzanInc.Peak.Sdk.Unity (Unity adapter)
├── examples/                        Godot + console smoke
├── upstream-snapshots/              Read-only copies of port sources:
│   ├── peak-sdk-unity/              Pinned @ commit SHA in upstream-snapshots/SOURCES.md
│   ├── turnkey-sdk-unity/           Pinned @ commit SHA
│   ├── turnkey-official-src/        @turnkey/{crypto,api-key-stamper,http} pin
│   └── peak-server-openapi/         peak-server OpenAPI spec @ tag
├── docs/
│   ├── architecture.md
│   ├── development.md
│   ├── security/
│   │   ├── storage-threat-model.md
│   │   └── crypto-port-policy.md
│   └── sync-rules.md                OpenAPI + Unity-source sync workflow
├── plans/
│   └── plans-peak-sdk-csharp.md     Active port plan (decisions, status, adjudication)
├── codex-crypto-reviews/            Per-file Codex multi-round review evidence
└── .github/workflows/
    ├── csharp-ci.yml                Build + test matrix
    └── csharp-publish.yml           (deferred) NuGet publish
```

## License

[MIT](LICENSE). Crypto primitives are ported from Turnkey's open-source
TypeScript SDK under its license terms; see the upstream snapshot for the
original notices.
