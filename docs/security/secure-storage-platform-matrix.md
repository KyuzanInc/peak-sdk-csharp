# Secure storage platform matrix

Authoritative table of which `ISecureStorage` implementation this C# NuGet
repository ships for each .NET host. The separate Unity UPM `IStorage` adapter
is documented below and does not change this table. The threat model
([storage-threat-model.md](storage-threat-model.md)) governs why each row
is what it is.

| Host | Package | Class | `IsAvailable` | Persistence backend |
|---|---|---|---|---|
| Windows .NET 8 (`net8.0-windows`) | `KyuzanInc.Peak.Sdk` (`net8.0-windows` TFM) | `DpapiSecureStorage` | `true` | Per-user DPAPI (`System.Security.Cryptography.ProtectedData`); blob path `%LOCALAPPDATA%\KyuzanInc\PeakSdk\<namespace>\` |
| Linux .NET 8 | `KyuzanInc.Peak.Sdk` (no extra package) | `UnavailableSecureStorage` | `false` | None in v0.1.0. v0.2+ may add a `libsecret` adapter package. |
| macOS .NET 8 | `KyuzanInc.Peak.Sdk` | `UnavailableSecureStorage` | `false` | None in v0.1.0. v0.2+ may add a Keychain Services adapter. |
| Godot 4.x / console | `KyuzanInc.Peak.Sdk` | `UnavailableSecureStorage` | `false` | Same as host OS row above. |
| Unity iOS / Android (`netstandard2.1`) | `KyuzanInc.Peak.Sdk` | No built-in mobile `ISecureStorage`; `UnavailableSecureStorage` is the explicit placeholder | `false` | Use the separate Unity UPM opt-in described below. It implements `IStorage`, not `ISecureStorage`. |
| Unity standalone (`netstandard2.1`) | `KyuzanInc.Peak.Sdk` | No built-in Unity `ISecureStorage`; `UnavailableSecureStorage` is the explicit placeholder | `false` | `DpapiSecureStorage` is compiled only for the `net8.0-windows` TFM, not Unity's `netstandard2.1` build. |
| .NET MAUI iOS / Android | not in v0.1.0 | n/a | n/a | v0.2+ may add `KyuzanInc.Peak.Sdk.Maui` |

## Planned separate Unity UPM storage in upcoming v0.8.0

[`com.kyuzan.peak-sdk-unity`](https://github.com/KyuzanInc/peak-sdk-unity)
is a separate UPM package. Its upcoming v0.8.0 release is planned to make
`EncryptedPlayerPrefsStorage` implement this repo's `IStorage` contract as an
explicit opt-in; the default Unity storage will remain `InMemoryStorage`.
Until that release is published, the rows below describe release-candidate
behavior, not a currently available package guarantee.

| Unity runtime | DEK protection used by `EncryptedPlayerPrefsStorage` | Authentication UI | Backup boundary |
|---|---|---|---|
| iOS player | A 32-byte DEK in iOS Keychain, non-synchronizable and `kSecAttrAccessibleWhenUnlockedThisDeviceOnly` | The SDK configures no access-control, user-presence, or biometry policy. A locked-device access fails transiently without showing authentication UI. | The Keychain DEK is device-only and does not synchronize; PlayerPrefs contains ciphertext only. |
| Android player | A non-exportable AES-256-GCM KEK in Android Keystore wraps a random 32-byte DEK; the wrapped record is atomically stored as `noBackupFilesDir/peak.sdk.dek.wrapped.v1` | `setUserAuthenticationRequired(false)`; no `BiometricPrompt`, and StrongBox is not required. | The wrapped DEK is outside backup. Consumers must also exclude PlayerPrefs/SharedPreferences ciphertext using the Unity package's documented backup rules. |
| Editor / desktop player | Software-derived `InterimDeviceBoundKeyProvider` | None | Development-only fallback; it is not an OS-protected production backend for High or Critical assets. |

The planned UPM release will not ship C# `KeychainSecureStorage` or
`KeyStoreSecureStorage` classes and will provide no `ISecureStorage`
implementation with `IsAvailable == true`.

## Consumer behaviour when `IsAvailable` is `false`

Once v0.8.0 is released, Unity iOS/Android consumers that deliberately need
persistence will be able to use the UPM opt-in above. For current releases and
other hosts without an available secure backend:

1. **Recommended**: persist nothing. Re-authenticate via OTP on each
   process start. Default `InMemoryStorage` already does this.
2. **Acceptable for trusted environments only**: wire your own
   plaintext-on-disk `IStorage`, accepting the threat-model trade-off
   in writing.
3. **Not recommended**: plaintext persistence. The planned
   `UnsafePlaintextPlayerPrefsStorage` has not shipped in any package.

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
  original plan D20 (currently deferred — see
  [plans/plans-peak-sdk-csharp.md](../../plans/plans-peak-sdk-csharp.md)).
  This prospective NuGet artifact is distinct from the already separate
  `com.kyuzan.peak-sdk-unity` UPM package.
- v1.0: TPM-backed key wrapping investigation.
