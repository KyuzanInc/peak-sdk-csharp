# peak-sdk-csharp

`KyuzanInc.Peak.Sdk` is a .NET SDK for the Peak embedded-wallet platform. It
supports modern .NET hosts, including console applications and .NET MAUI.

- Stable version: `1.0.0`
- Distribution: private GitHub Packages for explicitly authorized consumers
- Dependency: `KyuzanInc.Turnkey.Sdk [1.0.0]`

Source availability does not grant package access. GitHub Packages
authentication requires a classic PAT with `read:packages` and explicit package
access, or an authorized private repository `GITHUB_TOKEN`. See
[docs/development.md](docs/development.md) for setup.

## Packages

This repository builds multiple NuGet artifacts.

| Package | TFM | Purpose |
|---|---|---|
| `KyuzanInc.Peak.Sdk` | `netstandard2.1;net8.0;net8.0-windows` | Peak SDK: `PeakClient`, OTP login, account/private-key services, storage abstractions, and Windows DPAPI secure storage. |
| `KyuzanInc.Peak.PublicApiClient` | `netstandard2.1;net8.0` | Auto-generated OpenAPI client used for spec-drift and DTO coverage checks; it is not a runtime SDK dependency. |
| `KyuzanInc.Peak.Sdk.Unity` | `netstandard2.1` | Unity platform adapter with OS secure storage support. |

## Dependency

| Dependency | Source | Pin |
|---|---|---|
| `KyuzanInc.Turnkey.Sdk` | [KyuzanInc/turnkey-sdk-csharp](https://github.com/KyuzanInc/turnkey-sdk-csharp) on private GitHub Packages | `[1.0.0]` (exact, via CPM and committed `packages.lock.json`) |

The Turnkey port (Crypto, ApiKeyStamper, HTTP, and encoding) is maintained in a
separate repository and reaches this SDK through a deliberate, reviewed version
bump.

## Distribution boundary

- The source repository is public; `KyuzanInc.Peak.Sdk` is a private package.
- Private GitHub Packages access is granted only to explicitly authorized
  consumers.
- Public GitHub Releases expose `release-checksums.txt` only, never `.nupkg` or
  `.snupkg` files.
- This project does not publish packages to nuget.org.
- The Turnkey crypto port remains in
  [`KyuzanInc/turnkey-sdk-csharp`](https://github.com/KyuzanInc/turnkey-sdk-csharp).
- This repository is not a 1:1 mirror of `peak-sdk-browser`; browser-only flows
  remain outside the current SDK surface.

## Quick start

For consumers with GitHub Packages authentication and explicit package access:

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

The SDK returns server responses as nullable DTOs. Validate required identity
and address fields before displaying them or using them in a transaction.

For maintainers, see [docs/development.md](docs/development.md) for local
build/test steps, [CONTRIBUTING.md](CONTRIBUTING.md) for contribution checks,
and [docs/release-process.md](docs/release-process.md) for the release runbook.

## Repository layout

```
peak-sdk-csharp/
├── peak-sdk-csharp.sln              Root solution
├── Directory.Build.props            Shared MSBuild settings
├── Directory.Packages.props         Central package versions
├── nuget.config                     Package-source mapping
├── packages/                        SDK and generated-client projects
├── examples/                        Consumer examples
├── upstream-snapshots/              Public provenance records for generated sources
├── docs/                            Architecture, development, security, and ADRs
└── .github/workflows/               CI and package publication workflows
```

## License

[MIT](LICENSE). Turnkey crypto primitives are consumed from the external
`KyuzanInc.Turnkey.Sdk` package and licensed under its original Turnkey terms.
