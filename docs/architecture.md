# Architecture

`peak-sdk-csharp` is a four-package .NET solution that ports the
Unity-only `peak-sdk-unity` + `turnkey-sdk-unity` SDKs to generic
NuGet packages.

## Layering

```
+----------------------------------------------------------+
|  Application code (consumer's project)                   |
+----------------------------------------------------------+
|  KyuzanInc.Peak.Sdk        (public surface, generic)     |
|  - PeakClient, AuthenticatedPeakClient                   |
|  - IStorage / ISecureStorage / DpapiSecureStorage        |
|  - AuthService / AccountService / PrivateKeyService      |
|  - PeakError + PeakErrorCode                             |
|  - DTOs (hand-designed, no generator types leak)         |
+----------------------------------------------------------+
|  KyuzanInc.Peak.Sdk.Unity  (Unity-only adapter)          |
|  - UnsafePlaintextPlayerPrefsStorage (opt-in)            |
|  - KeychainSecureStorage / KeyStoreSecureStorage         |
+----------------------------------------------------------+
|  KyuzanInc.Peak.PublicApiClient                          |
|  - internal-only OpenAPI codegen                         |
|  - referenced as <IsPrivateAssets="all">                  |
+----------------------------------------------------------+
|  KyuzanInc.Turnkey.Sdk                                   |
|  - Crypto (HPKE / ECDSA / HKDF / Bundle parse)           |
|  - ApiKeyStamper (P-256 ECDSA + DER + low-S)             |
|  - Http (typed activity request signers)                 |
|  - Encoding (hex / base58 / base58check)                 |
+----------------------------------------------------------+
|  BouncyCastle.Cryptography 2.5.0   (primitive backend)   |
|  System.Text.Json 8.0.5            (source-gen context)  |
+----------------------------------------------------------+
```

The boundary that matters for security review is the
`KyuzanInc.Turnkey.Sdk` ↔ BouncyCastle line. Everything above
`Turnkey.Sdk` is business logic; everything below it is well-known
primitive code we did not write.

## Target framework strategy

| Package | TFMs |
|---|---|
| `KyuzanInc.Turnkey.Sdk` | `netstandard2.1;net8.0` |
| `KyuzanInc.Peak.PublicApiClient` | `netstandard2.1;net8.0` |
| `KyuzanInc.Peak.Sdk` | `netstandard2.1;net8.0;net8.0-windows` (last one only conditionally compiles `DpapiSecureStorage`) |
| `KyuzanInc.Peak.Sdk.Unity` | `netstandard2.1` (Unity 2021.2+ compatible) |

`netstandard2.1` is the lowest common denominator that supports
Unity 2021.2 LTS and modern .NET. `net8.0` is enabled so consumers on
modern .NET get faster primitives and source-generated JSON paths.

## Sync vs async

`PeakClient` exposes **both** synchronous and asynchronous variants
for every method that is naturally async (network or storage-IO).
Decision D2 in the plan: the Unity shim layer is sync, modern .NET
consumers prefer async; we serve both rather than blocking one on the
other.

## Logging

All services accept `ILogger<T>` via constructor injection; the
default is `NullLogger<T>.Instance`. Logging never emits raw key
material; `PeakError.LogContext` carries the redaction-aware payload.

## OpenAPI codegen

`KyuzanInc.Peak.PublicApiClient` is an OpenAPI-generated client. It is
deliberately not exposed to consumers (`<IsPrivateAssets="all"/>` on
its `<PackageReference>`). The hand-designed DTOs in
`KyuzanInc.Peak.Sdk` wrap the generated types so that a codegen
template change does not break the public surface.

Source spec: `upstream-snapshots/peak-server-openapi/public-api.yaml`,
pinned to a `peak-server` tag (see
[docs/sync-rules.md](sync-rules.md)).

## Test layout

```
packages/<name>-csharp/tests/
  <name>-csharp.Tests.csproj         net8.0 (latest runtime)
  *Tests.cs                          one xUnit file per source file
  Fixtures/                          committed test fixtures (text files)
    README.md                        cites the origin of each fixture
  E2E/                               env-gated tests; run only when secrets present
```

CI runs the test suite on `ubuntu-latest`, `windows-latest`, and
`macos-latest`. E2E tests require `TURNKEY_TEST_ORG_API_KEY` and
`TURNKEY_TEST_ORG_ID` GitHub Actions secrets; absence means the suite
is skipped, not failed.
