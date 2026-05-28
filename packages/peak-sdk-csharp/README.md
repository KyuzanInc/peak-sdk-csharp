# peak-sdk-csharp (internal README)

The `KyuzanInc.Peak.Sdk` package. For the NuGet-facing README see
[README.public.md](README.public.md).

## Layout

| Path | Contents |
|---|---|
| `src/PeakClient.cs` | Public entry point (Initialize / OTP login / Authenticate) |
| `src/AuthenticatedPeakClient.cs` | Authenticated surface, mirrors Unity's AuthenticatedPeakSdk |
| `src/PeakError.cs` | Unified error type + PeakErrorCode string constants |
| `src/PeakJsonContext.cs` | System.Text.Json source-generated context |
| `src/Services/` | AuthService / AccountService / PrivateKeyService |
| `src/Models/` | DTOs (camelCase JSON in/out via source-gen context) |
| `src/Storage/` | IStorage / ISecureStorage / InMemoryStorage / SessionData |
| `src/Utils/` | SessionJwt, IPeakHttpClient, DefaultPeakHttpClient |
| `tests/` | xUnit unit + integration tests + TurnkeyWireFormatSmokeTests |

## Port philosophy

This package is a port of `upstream-snapshots/peak-sdk-unity/Runtime/`.
Logic is 1:1 with the Unity port; mechanical changes:

- `UniTask<T>` → `Task<T>`
- `UnityEngine.Debug.Log` → `ILogger<T>` (default `NullLogger<T>.Instance`)
- `UnityWebRequest` → `System.Net.Http.HttpClient`
- `JsonUtility.ToJson` / `FromJson` → `JsonSerializer.Serialize` /
  `Deserialize` via the source-generated `PeakJsonContext`
- All `[Serializable]` field-based POCOs converted to auto-property
  POCOs (so source-gen can introspect them)

Turnkey-typed members (`Http.SignedRequest`, `Crypto.KeyPair`, etc.)
come from the external `KyuzanInc.Turnkey.Sdk` package via the
`Turnkey.*` namespace. The storage-threat-model document in
`docs/security/` governs security-critical paths.

## Run tests

```
dotnet test ../../peak-sdk-csharp.sln -c Release \
  --filter "FullyQualifiedName~KyuzanInc.Peak.Sdk.Tests"
```

Run only the wire-format smoke against the pinned Turnkey package:

```
dotnet test ../../peak-sdk-csharp.sln -c Release \
  --filter "FullyQualifiedName~TurnkeyWireFormatSmokeTests"
```
