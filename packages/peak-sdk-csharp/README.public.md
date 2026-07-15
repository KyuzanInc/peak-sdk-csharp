# KyuzanInc.Peak.Sdk

.NET SDK for the Peak embedded-wallet platform. OTP login, account
listing, private key import / export, session JWT handling.

## What this is

A generic-NET port of `peak-sdk-unity` by Kyuzan Inc. Semantic
equivalent of the Unity public surface plus the OTP login flow
shared with `peak-sdk-browser`. Targets `netstandard2.1` (Unity
2021.2+ compatible) and `net8.0`.

## What this is NOT

This is not the canonical TypeScript SDK. Browser-specific flows
(Google OAuth, IndexedDB Turnkey-key-pair storage) are out of v0.1.0
scope and tracked separately.

Persistent storage is opt-in: the default `IStorage` implementation
is `InMemoryStorage` (memory only, lost on process exit). This NuGet
package's only built-in available `ISecureStorage` is
`DpapiSecureStorage` in its `net8.0-windows` build. Other target
frameworks provide `UnavailableSecureStorage` with
`IsAvailable == false`; consumers can inject their own `IStorage` or
`ISecureStorage` implementation when needed.

There is no C# `KyuzanInc.Peak.Sdk.Unity` artifact and no
`KeychainSecureStorage` or `KeyStoreSecureStorage` class. For Unity,
the separate
[`com.kyuzan.peak-sdk-unity`](https://github.com/KyuzanInc/peak-sdk-unity)
UPM package has an upcoming v0.8.0 release planned to offer opt-in
`EncryptedPlayerPrefsStorage`. It will implement `IStorage`, not
`ISecureStorage`: PlayerPrefs will store a `peak.enc.v1` encrypted value,
while iOS Keychain or Android Keystore will protect the data-encryption
key. The planned storage path will not request Face ID, Touch ID,
Android biometrics, or a device passcode. Its default will remain
volatile `InMemoryStorage`. Until v0.8.0 is released, do not treat this
release-candidate behavior as an available package guarantee.

See [`docs/security/storage-threat-model.md`](https://github.com/KyuzanInc/peak-sdk-csharp/blob/main/docs/security/storage-threat-model.md)
for the complete platform and residual-risk boundaries.

## Consumer setup: GitHub Packages auth required

`KyuzanInc.Peak.Sdk` depends on the
[`KyuzanInc.Turnkey.Sdk`](https://github.com/KyuzanInc/turnkey-sdk-csharp)
package, which ships from GitHub Packages until it lands on nuget.org.
Your project's `nuget.config` (or `~/.nuget/NuGet/NuGet.Config`) must
include the GitHub Packages source with a PAT that has the
`read:packages` scope and access to the
`KyuzanInc/turnkey-sdk-csharp` package:

```
# Make sure the user-level NuGet config exists as valid XML
# (brand-new profiles do not have it yet, and `add source` rejects
# an empty file with "Root element is missing").
mkdir -p ~/.nuget/NuGet
[ -s ~/.nuget/NuGet/NuGet.Config ] || cat > ~/.nuget/NuGet/NuGet.Config <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
</configuration>
XML

dotnet nuget add source \
  https://nuget.pkg.github.com/KyuzanInc/index.json \
  --name github-kyuzan \
  --username <your-github-username> \
  --password "$GITHUB_TOKEN" \
  --store-password-in-clear-text \
  --configfile ~/.nuget/NuGet/NuGet.Config
```

Use `dotnet nuget update source github-kyuzan ...` (same arg shape,
without `--name`) on later token refreshes. CI runners need
`permissions: packages: read` and the same authentication step
(`update source` when the repo's `nuget.config` already declares the
source). See
[`docs/development.md`](https://github.com/KyuzanInc/peak-sdk-csharp/blob/main/docs/development.md)
for the full setup, including `packageSourceMapping` for shops that
restrict resolution per source.

## Install

```
dotnet add package KyuzanInc.Peak.Sdk
```

## Quick start

```csharp
using KyuzanInc.Peak.Sdk;

// Step 1 — initialise
var client = PeakClient.Initialize(new PeakClientOptions
{
    ProjectApiKey = "...",
    ApiUrl        = "https://api.peak.xyz",
    // Storage      = new MyDpapiStorage(),    // optional
    // HttpClient   = customHttpClient,         // optional
    // LoggerFactory = loggerFactory,           // optional
});

// Step 2 — OTP login flow
var init   = await client.InitOtpLoginAsync("user@example.com");
var login  = await client.CompleteOtpLoginAsync("user@example.com", init.OtpId, "123456");

// Step 3 — authenticated calls
var auth     = client.Authenticate();
var accounts = await auth.ListAccountsAsync();
foreach (var account in accounts)
{
    var addresses = await auth.ListAccountAddressesAsync(account.Id);
    System.Console.WriteLine($"{account.DisplayName}: {addresses.Length} addresses");
}
```

## Errors

Every failure surfaces as `PeakError`, carrying a string `Code` from
`PeakErrorCode` (`SDK_SESSION_EXPIRED`, `SDK_HTTP_ERROR`, etc.) and
optionally an `ApiResponseContext` for log redaction. Use
`PeakError.IsAny(ex)` for type checks; this matches the TS family's
`isPeakError(error)` guard.

## License

[MIT](https://github.com/KyuzanInc/peak-sdk-csharp/blob/main/LICENSE).
