# Compatibility coverage map

This map links the retained public C# surface to its implementation and
compatibility evidence. The sanitized OpenAPI contract and generated client are
build-time checks; they are not the runtime API.

| Retained surface | Role | Compatibility evidence |
|---|---|---|
| `PeakClient` | Unauthenticated entry point, configuration, OTP login, and session creation | `PeakClientTests.cs`, hand-written request/response DTOs, generated DTO coverage checks |
| `AuthenticatedPeakClient` | Authenticated account, address, and private-key operations | authenticated client/service tests and hand-written response DTOs |
| `PeakCrypto` | Peak-owned wrapper over Turnkey key and bundle operations | `PeakCryptoTests.cs` and `TurnkeyWireFormatSmokeTests.cs` |
| `IStorage`, `ISecureStorage` | Consumer-provided session and secret persistence boundaries | storage contract tests and public security documentation |
| `InMemoryStorage` | Volatile storage implementation | `InMemoryStorageTests.cs` |
| `DpapiSecureStorage`, `UnavailableSecureStorage` | Platform secure-storage implementation and fail-closed fallback | platform-focused storage tests and `docs/security/secure-storage-platform-matrix.md` |
| Hand-written DTOs in `Models/Models.cs` | Runtime System.Text.Json request/response contract | `PeakJsonContext`, response serialization tests, and `GeneratedDtoContractTests.cs` |
| `TurnkeyWireFormatSmokeTests.cs` | Exact `[1.0.0]` Turnkey dependency boundary | Verifies the signing and crypto wire formats consumed by Peak |

## Intentionally unsupported browser behavior

- Browser-only OAuth orchestration is unsupported because it depends on browser
  navigation, callback routing, and origin policy. Applications may implement
  that UI flow outside the generic SDK and pass the resulting supported inputs
  to the C# surface.
- IndexedDB behavior is unsupported because IndexedDB is a browser persistence
  API. Generic .NET and Unity consumers instead provide `IStorage` and
  `ISecureStorage` implementations appropriate to their platform.

These exclusions keep browser lifecycle and persistence policy out of the
portable SDK while preserving explicit extension boundaries.
