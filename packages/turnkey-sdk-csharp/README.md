# turnkey-sdk-csharp (internal README)

This is the internal README for the `KyuzanInc.Turnkey.Sdk` package.
For the NuGet-facing README, see [README.public.md](README.public.md).

## What lives here

| Path | Contents |
|---|---|
| `src/Encoding.cs` | Port of `@turnkey/encoding@0.6.0` |
| `src/Crypto.cs` | Port of `@turnkey/crypto@2.8.9` (HPKE, HKDF, ECDSA, bundle parse, JWT verify) |
| `src/CryptoConstants.cs` | BouncyCastle curve name + P-256 parameters |
| `src/ApiKeyStamper.cs` | Port of `@turnkey/api-key-stamper@0.6.0` |
| `src/Http.cs` | Port of `@turnkey/http@3.16.0` (stamping subset) |
| `src/TurnkeyJsonContext.cs` | System.Text.Json source-generated context (AOT safe) |
| `tests/` | xUnit test suite + fixtures |

## Port philosophy

This package is a 1:1 logical port of Turnkey's official TypeScript
SDK. The pinned upstream versions are recorded in
[../../codex-crypto-reviews/turnkey-source-pins.md](../../codex-crypto-reviews/turnkey-source-pins.md);
the Unity port-base commits used as the immediate source are in
[../../codex-crypto-reviews/unity-source-pins.md](../../codex-crypto-reviews/unity-source-pins.md).

The crypto-port-policy document at
[../../docs/security/crypto-port-policy.md](../../docs/security/crypto-port-policy.md)
is normative: do not touch `src/Crypto.cs`, `src/ApiKeyStamper.cs`,
`src/Http.cs`, or `src/Encoding.cs` without following the multi-round
Codex review process documented there.

## Tests

```
dotnet test ../../peak-sdk-csharp.sln -c Release \
  --filter "FullyQualifiedName~KyuzanInc.Turnkey.Sdk.Tests"
```

E2E tests (currently the `TurnkeyWhoamiTests` placeholder in
`tests/E2E/`) are gated on `TURNKEY_TEST_ORG_API_KEY` and
`TURNKEY_TEST_ORG_ID`. CI runs them only when those secrets are set.

## Public surface invariants

- Public API mirrors the Unity surface in
  `upstream-snapshots/turnkey-sdk-unity/Runtime/`. We deliberately do
  NOT introduce `IApiKeyStamper` or top-level DTOs — the nested DTOs
  inside `Http` and the concrete `ApiKeyStamper` class are the
  contract. See plan decision D16.
- Public methods are stateless or carry their own state explicitly.
  No DI / `ILogger<T>` is required at this layer; consumers wire
  logging at the SDK layer above.
- Errors throw `Exception` or `ArgumentException` — semantic
  equivalents of the TypeScript `throw new Error(...)`. The SDK
  layer above wraps these into `PeakError`.
