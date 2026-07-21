# Architecture

`peak-sdk-csharp` is a multi-package .NET solution for the generic Peak SDK.
The separate `peak-sdk-unity` repository is a downstream adapter; its source
tree is intentionally not retained here. The Turnkey crypto / API key stamping
layer is consumed from the external
[`KyuzanInc.Turnkey.Sdk`](https://github.com/KyuzanInc/turnkey-sdk-csharp)
private package at the exact stable `[1.0.0]` dependency boundary; it is not
built in this repository.

## Layering

```
+----------------------------------------------------------+
|  Application code (consumer's project)                   |
+----------------------------------------------------------+
|  KyuzanInc.Peak.Sdk        (public surface, generic)     |
|  - PeakClient, AuthenticatedPeakClient                   |
|  - IStorage / ISecureStorage / DpapiSecureStorage        |
|  - Services layer (Auth/Account/PrivateKey, internal)    |
|  - PeakCrypto (thin public wrapper over Turnkey crypto)  |
|  - PeakError + PeakErrorCode                             |
|  - DTOs hand-designed; generated types kept internal     |
+----------------------------------------------------------+
|  KyuzanInc.Peak.Sdk.Unity  (downstream adapter support)  |
|  - UnsafePlaintextPlayerPrefsStorage (opt-in)            |
|  - KeychainSecureStorage / KeyStoreSecureStorage         |
+----------------------------------------------------------+
|  KyuzanInc.Peak.PublicApiClient                          |
|  - internal-only OpenAPI codegen                         |
|  - build-time only; not a runtime/package dependency     |
+----------------------------------------------------------+
|  KyuzanInc.Turnkey.Sdk [1.0.0] (private NuGet package)   |
|  - Crypto (HPKE / ECDSA / HKDF / Bundle parse)           |
|  - ApiKeyStamper (P-256 ECDSA + DER + low-S)             |
|  - Http (typed activity request signers)                 |
|  - Encoding (hex / base58 / base58check)                 |
+----------------------------------------------------------+
|  BouncyCastle.Cryptography 2.5.0   (transitive backend)  |
|  System.Text.Json 8.0.5            (source-gen context)  |
+----------------------------------------------------------+
```

`KyuzanInc.Turnkey.Sdk` `[1.0.0]` is an exact external dependency: this repo does
not build, ship, or audit its crypto code. The security review boundary
for Peak SDK code is the `KyuzanInc.Peak.Sdk` ↔ `Turnkey.*` line — the
API surface we consume from the Turnkey package. Wire-format
expectations on that surface are pinned by
`packages/peak-sdk-csharp/tests/TurnkeyWireFormatSmokeTests.cs`.

`PeakCrypto` re-exposes the client-side import/export crypto slice of
`Turnkey.Crypto` / `Turnkey.Encoding` (P-256 public-key derivation,
target key-pair generation, import-bundle encryption, export-bundle
decryption, hex encode/decode) on the Peak surface with Peak-owned
param/result types. It is a thin delegation wrapper — no crypto logic of
its own — so downstream consumers depend only on `KyuzanInc.Peak.Sdk` and
never reference `Turnkey.*` directly. The
test-only `dangerouslyOverrideSignerPublicKey` knob is intentionally not
part of the Peak surface. DLL-level internalization of Turnkey remains
out of scope: it stays a normal transitive dependency.

## Target framework strategy

| Package | TFMs |
|---|---|
| `KyuzanInc.Peak.PublicApiClient` | `netstandard2.1;net8.0` |
| `KyuzanInc.Peak.Sdk` | `netstandard2.1;net8.0;net8.0-windows` (last one only conditionally compiles `DpapiSecureStorage`) |
| `KyuzanInc.Peak.Sdk.Unity` | `netstandard2.1` (Unity 2021.2+ compatible) |

`netstandard2.1` is the lowest common denominator that supports
Unity 2021.2 LTS and modern .NET. `net8.0` is enabled so consumers on
modern .NET get faster primitives and source-generated JSON paths.
`KyuzanInc.Turnkey.Sdk` targets `netstandard2.1;net8.0`, which is a
strict superset of what we need.

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
**build-time only**: the SDK does not reference it at runtime and does
not ship it in the `KyuzanInc.Peak.Sdk` package. It exists to detect
spec drift (the `openapi-client-drift` CI job regenerates and diffs it)
and to back the `GeneratedDtoContractTests` field-coverage check. The
SDK's public DTOs are hand-written System.Text.Json types
(`Models/Models.cs`) deserialized directly via `PeakJsonContext`; they
do not wrap the generated types (reverted per issue #18).

Source spec: `upstream-snapshots/peak-server-openapi/public-api.yaml`,
imported from `KyuzanInc/peak` `main` at an exact commit and then sanitized as a
public contract. Provenance and both imported/public checksums are recorded in
[`docs/compatibility/upstream-pins.md`](compatibility/upstream-pins.md); sync
rules live in [`docs/sync-rules.md`](sync-rules.md).

## Test layout

```
packages/<name>-csharp/tests/
  <name>-csharp.Tests.csproj         net8.0 (latest runtime)
  *Tests.cs                          one xUnit file per source file
  TurnkeyWireFormatSmokeTests.cs     pins the external Turnkey surface
  E2E/                               env-gated tests; run only when secrets present
```

CI runs the test suite on `ubuntu-latest`, `windows-latest`, and
`macos-latest`. E2E tests require `TURNKEY_TEST_ORG_API_KEY` and
`TURNKEY_TEST_ORG_ID` GitHub Actions secrets; absence means the suite
is skipped, not failed.
