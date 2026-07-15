# Storage threat model

This document is normative for the `peak-sdk-csharp` storage stack.
Anything that wants to be a default in this SDK MUST be acceptable
under the constraints below; otherwise it is opt-in only, with the
opt-in mechanism described in this file.

## Trust boundaries

A consumer of this SDK is one of:

- **Server / backend.** No user device involved. Session JWT and
  target private key never leave the trusted environment. Storage
  threats are server-side (cred dumps, log leaks, container snapshots).
- **Desktop / console application.** A user-controlled OS process
  with whatever filesystem and OS keychain access the user grants.
  Threats: disk image theft, malware running as the same user,
  uninstall residue.
- **Mobile (Unity, MAUI, Xamarin).** Same as desktop, plus app-data
  exposure on rooted / jailbroken devices, screen recording, OS-level
  backup that captures app sandboxes.
- **Game console.** Treated as desktop with a stricter security
  baseline supplied by the platform vendor.

## Asset taxonomy

| Asset | Sensitivity | Persistence pressure |
|---|---|---|
| Session JWT | High — grants every Turnkey activity until expiry | High; UX expects resume |
| Target private key (P-256) | Critical — used to stamp Turnkey activities including key export | High; same UX as JWT |
| Email address | Medium — PII | Low |
| Project API key | Medium — identifies the project to Peak, NOT a per-user secret | None at runtime; configured |

## Storage tiers

| Tier | Member implementations | Default? | Persistent? |
|---|---|---|---|
| `IStorage` (key/value, in-process) | `InMemoryStorage` | Yes | No |
| `ISecureStorage` (OS-protected) | `DpapiSecureStorage` (Windows `net8.0-windows`, `peak-sdk-csharp` core) | No; in v0.1.0 only on Windows (`net8.0-windows`) | Yes |
| Planned Unity UPM encrypted PlayerPrefs | `EncryptedPlayerPrefsStorage` in the upcoming separate `com.kyuzan.peak-sdk-unity` v0.8.0 release; planned to implement the core `IStorage` abstraction | No (explicit opt-in) | Yes |
| Unsafe plaintext | `UnsafePlaintextPlayerPrefsStorage` — **planned only, not implemented in any shipped package**; the opt-in guards below bind any future implementation | No | Yes |

`ISecureStorage` extends `IStorage`. `ISecureStorage.IsAvailable`
returns `false` when no platform-secure backend is wired (e.g. on a
generic Godot / console host on Linux or macOS, where the core SDK
ships no built-in implementation). Consumers MUST check `IsAvailable`
before persisting any High or Critical asset.

### Planned separate Unity UPM v0.8.0 boundary

[`com.kyuzan.peak-sdk-unity`](https://github.com/KyuzanInc/peak-sdk-unity)
has an upcoming v0.8.0 release that is planned to ship
`EncryptedPlayerPrefsStorage` as an explicitly selected `IStorage`. It will not
be a C# `ISecureStorage` implementation and will provide no `ISecureStorage`
with `IsAvailable == true`. No `KyuzanInc.Peak.Sdk.Unity`,
`KeychainSecureStorage`, or `KeyStoreSecureStorage` artifact is shipped by this
repository. Until v0.8.0 is released, this section describes release-candidate
behavior rather than a currently available package guarantee.

The Unity default is planned to remain unchanged: without explicit storage
injection, `InMemoryStorage` persists nothing. With encrypted persistence
selected in v0.8.0, values will retain the `peak.enc.v1` AES-256-GCM envelope
while mobile players use OS-protected DEK providers:

- **iOS:** a 32-byte DEK is stored in Keychain as non-synchronizable,
  `kSecAttrAccessibleWhenUnlockedThisDeviceOnly`. The provider sets no
  access-control, user-presence, or biometry policy and refuses authentication
  UI. A locked-device access is transient and can be retried after unlock.
- **Android:** a non-exportable AES-256-GCM Android Keystore KEK wraps a random
  32-byte DEK. The wrapped record is written atomically to
  `noBackupFilesDir/peak.sdk.dek.wrapped.v1`. User authentication is disabled,
  the SDK does not use `BiometricPrompt`, and StrongBox is not required.

The planned storage path will therefore not request Face ID, Touch ID, Android
biometrics, or a device passcode. This avoids an SDK-triggered prompt; it does
not protect against a compromised application process or a rooted / jailbroken
device.

In the planned release, transient Keychain, Keystore, lock-state, and I/O
failures will preserve existing key material and ciphertext for retry; writes
will fail rather than succeeding without encryption. Permanent key loss or a
permanently corrupt wrapped DEK will cause the provider to create a fresh key.
Existing PlayerPrefs ciphertext will then fail GCM authentication, be deleted,
and require a new login.

The v0.7.0 software seed will deliberately not be used to migrate credentials.
Only after the v0.8.0 native key has been created and durably read back will
the Unity adapter attempt to delete `peak.sdk.dek.seed`; cleanup failures will
be retried. The old ciphertext will be undecryptable under the new key and
removed, so upgraded users will log in once before persistence resumes with
the same `peak.enc.v1` envelope.

**Editor / desktop caveat.** The v0.8.0 Unity package will retain
`InterimDeviceBoundKeyProvider` there for development and test compatibility.
It derives a DEK in software from a seed and device identifier and will not be
an accepted OS-protected production backend for High or Critical assets.

## v0.1.0 release blockers

- The default-out-of-the-box experience MUST persist nothing. A
  freshly-installed SDK with no consumer wiring of `IStorage` uses
  `InMemoryStorage` and forgets credentials on process exit. We accept
  the UX hit; resume-after-restart is the consumer's deliberate
  choice.
- `UnsafePlaintextPlayerPrefsStorage` is planned only — it is NOT
  implemented in any shipped package, and shipping a plaintext tier is
  currently not intended (the Unity UPM mobile encrypted tier covers that
  persistence need). If it is ever implemented, it requires **both**
  of these to even be constructable:
  1. A compile-time symbol `PEAK_UNSAFE_STORAGE_OPT_IN` set on the
     **consumer's** csproj.
  2. A required constructor argument `bool acknowledgePlaintext`; the consumer
     must pass `acknowledgePlaintext: true`. The parameter has no default and
     the constructor throws on `false`. The
     existence of this argument is a release blocker — code review
     for v0.1.0 cut MUST verify the parameter is still required.
- `DpapiSecureStorage` is the only `ISecureStorage` in the core
  package. Linux, macOS, and other non-Windows .NET hosts get
  `ISecureStorage.IsAvailable == false`. The README and the platform
  matrix below MUST be in sync about this.

## Platform matrix

| Platform | `ISecureStorage` provider | `IsAvailable` | Notes |
|---|---|---|---|
| Windows .NET 8 (`net8.0-windows` TFM) | `DpapiSecureStorage` (core) | `true` | DPAPI per-user scope; only the `net8.0-windows` build compiles it |
| Linux .NET 8 | None (core) | `false` | v0.1.0 leaves this to consumers; v0.2+ may add `libsecret`-based provider |
| macOS .NET 8 | None (core) | `false` | v0.2+ may add Keychain provider |
| Unity iOS / Android (IL2CPP, core C# matrix) | None | `false` | The upcoming Unity UPM v0.8.0 opt-in is planned as encrypted PlayerPrefs `IStorage` with an OS-protected DEK, not a C# `ISecureStorage`; see above. |
| Unity standalone (Win/Mac/Linux, `netstandard2.1`) | None | `false` | The upcoming Unity UPM v0.8.0 release will use its software-derived interim provider for development only. |
| Godot 4.x / console | None (core) | `false` | same as Linux / macOS .NET host |

## Non-goals

- We do not provide a built-in cross-platform secret-store wrapper
  in v0.1.0. The cost / benefit of pulling in libsecret / Keychain
  Services / DPAPI under a single abstraction is high, and integrators
  can pick from existing libraries (e.g. `Plugin.Maui.SecureStorage`,
  `SecretStore.NetCore`).
- We do not encrypt-at-rest in `InMemoryStorage`. The instance is
  process memory; attackers with that level of access have stronger
  vectors.
- We do not implement TPM-backed keys in v0.1.0.

## Threat acceptance

| Threat | Default mitigation | Residual risk |
|---|---|---|
| Disk forensics on a stolen machine reads PlayerPrefs | Default storage is `InMemoryStorage`. The upcoming Unity UPM v0.8.0 mobile opt-in is planned to write only `peak.enc.v1` ciphertext to PlayerPrefs and protect its DEK with Keychain / Android Keystore. | OS protection does not defend a compromised process or rooted / jailbroken device. The Editor/desktop software fallback is development-only. |
| Malware on the same OS user reads app-data | DPAPI or the planned Unity UPM mobile Keychain / Keystore path prevents raw key export from ordinary file reads without requiring biometrics. | A process that can invoke the SDK while unlocked, or has root/jailbreak privileges, remains out of scope. |
| iCloud / Google Drive auto-backup snapshots app data | Default storage persists nothing. The upcoming Unity path is planned to use a non-synchronizable, device-only iOS Keychain DEK; on Android the KEK will stay in Keystore and the wrapped DEK in `noBackupFilesDir`, while consumers additionally exclude SharedPreferences ciphertext using the Unity package's backup rules. | Ciphertext may still appear in a backup, but the mobile DEK material must not travel with it. |
| OS-level key loss or wrapped-DEK corruption | DPAPI behavior is OS-defined. The planned Unity UPM mobile provider will create a fresh key only after permanent loss/corruption; the old ciphertext will then be purged. | The user must authenticate again; transient failures will preserve the existing ciphertext for retry. |
| Cold-boot attack on RAM | None at this layer | Out of scope |
