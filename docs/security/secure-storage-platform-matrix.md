# Secure storage platform matrix

Authoritative table of which `ISecureStorage` implementation activates on
which .NET host. The threat model
([storage-threat-model.md](storage-threat-model.md)) governs why each row
is what it is.

| Host | Package | Class | `IsAvailable` | Persistence backend |
|---|---|---|---|---|
| Windows .NET 8 (`net8.0-windows`) | `KyuzanInc.Peak.Sdk` (`net8.0-windows` TFM) | `DpapiSecureStorage` | `true` | Per-user DPAPI (`System.Security.Cryptography.ProtectedData`); blob path `%LOCALAPPDATA%\KyuzanInc\PeakSdk\<namespace>\` |
| Linux .NET 8 | `KyuzanInc.Peak.Sdk` (no extra package) | `UnavailableSecureStorage` | `false` | None in v0.1.0. v0.2+ may add a `libsecret` adapter package. |
| macOS .NET 8 | `KyuzanInc.Peak.Sdk` | `UnavailableSecureStorage` | `false` | None in v0.1.0. v0.2+ may add a Keychain Services adapter. |
| Godot 4.x / console | `KyuzanInc.Peak.Sdk` | `UnavailableSecureStorage` | `false` | Same as host OS row above. |
| Unity iOS | `KyuzanInc.Peak.Sdk.Unity` (PR 5 deliverable; deferred) | `KeychainSecureStorage` | `true` (planned) | iOS Security.framework via P/Invoke (Unity context) |
| Unity Android | `KyuzanInc.Peak.Sdk.Unity` (deferred) | `KeyStoreSecureStorage` | `true` (planned) | AndroidKeyStore via JNI (`AndroidJavaObject`, Unity context) |
| Unity standalone Windows | `KyuzanInc.Peak.Sdk` | `DpapiSecureStorage` | `true` | DPAPI (same as core Windows row) |
| Unity standalone Linux / macOS | `KyuzanInc.Peak.Sdk` | `UnavailableSecureStorage` | `false` | Same as desktop row |
| .NET MAUI iOS / Android | not in v0.1.0 | n/a | n/a | v0.2+ may add `KyuzanInc.Peak.Sdk.Maui` |

## Consumer behaviour when `IsAvailable` is `false`

1. **Recommended**: persist nothing. Re-authenticate via OTP on each
   process start. Default `InMemoryStorage` already does this.
2. **Acceptable for trusted environments only**: wire your own
   plaintext-on-disk `IStorage`, accepting the threat-model trade-off
   in writing.
3. **Not recommended**: `UnsafePlaintextPlayerPrefsStorage` (Unity
   adapter, deferred). Requires compile symbol + explicit ack flag and
   is intended for migration / parity with the old Unity SDK only.

## Why we deliberately ship the unavailable placeholder

A C#-typed `ISecureStorage` is more useful than a runtime null check.
`UnavailableSecureStorage.IsAvailable == false` is the explicit signal;
all its methods throw `PeakError(SDK_INVALID_ARGUMENT)` so a consumer
that ignores the flag fails loudly instead of silently writing to a
no-op store.

## Roadmap

- v0.2: investigate `KyuzanInc.Peak.Sdk.Linux` with `libsecret` (DBus-only
  hosts), `KyuzanInc.Peak.Sdk.Mac` with Keychain Services, and a unified
  `.NET MAUI` adapter (`KyuzanInc.Peak.Sdk.Maui`).
- v0.2: ship the Unity adapter (`KyuzanInc.Peak.Sdk.Unity`) per the
  original plan D20 (currently deferred — see plans/plans-peak-sdk-csharp.md).
- v1.0: TPM-backed key wrapping investigation.
