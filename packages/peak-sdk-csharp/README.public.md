# KyuzanInc.Peak.Sdk

`KyuzanInc.Peak.Sdk` is a .NET SDK for the Peak embedded-wallet platform. It
supports OTP login, account listing, private-key import/export, and session JWT
handling.

- Stable version: `1.0.0`
- Distribution: private GitHub Packages for explicitly authorized consumers
- Dependency: `KyuzanInc.Turnkey.Sdk [1.0.0]`

Source availability does not grant package access. GitHub Packages
authentication requires a classic PAT with `read:packages` and explicit package
access, or an authorized private repository `GITHUB_TOKEN`.

## Storage

Persistent storage is opt-in. The default `IStorage` implementation is
`InMemoryStorage`, so data is lost on process exit. Supply your own `IStorage`
implementation for persistence. See the
[storage threat model](https://github.com/KyuzanInc/peak-sdk-csharp/blob/main/docs/security/storage-threat-model.md).

## GitHub Packages authentication

`KyuzanInc.Peak.Sdk` and its exact
[`KyuzanInc.Turnkey.Sdk [1.0.0]`](https://github.com/KyuzanInc/turnkey-sdk-csharp)
dependency are private GitHub Packages packages. Configure the source in your
user-level NuGet config with a classic PAT that has `read:packages` and explicit
package access:

```bash
dotnet nuget add source https://nuget.pkg.github.com/KyuzanInc/index.json \
  --name github-kyuzan \
  --username <your-github-username> \
  --password "$GITHUB_TOKEN" \
  --store-password-in-clear-text \
  --configfile ~/.nuget/NuGet/NuGet.Config
```

An authorized private repository `GITHUB_TOKEN` may be used in CI. See the
[development guide](https://github.com/KyuzanInc/peak-sdk-csharp/blob/main/docs/development.md)
for the full setup and troubleshooting.

## Install

```bash
dotnet add package KyuzanInc.Peak.Sdk --version 1.0.0
```

## Quick start

```csharp
using KyuzanInc.Peak.Sdk;

var client = PeakClient.Initialize(new PeakClientOptions
{
    ProjectApiKey = "...",
    ApiUrl = "https://api.peak.xyz",
});

var init = await client.InitOtpLoginAsync("user@example.com");
var login = await client.CompleteOtpLoginAsync("user@example.com", init.OtpId, "123456");
var auth = client.Authenticate();
var accounts = await auth.ListAccountsAsync();
```

## Errors

Failures surface as `PeakError`, with a string `Code` from `PeakErrorCode` and
an optional `ApiResponseContext` for log redaction. Use `PeakError.IsAny(ex)`
for type checks.

## License

[MIT](https://github.com/KyuzanInc/peak-sdk-csharp/blob/main/LICENSE).
