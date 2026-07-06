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
| `ISecureStorage` (OS-protected) | `DpapiSecureStorage` (Windows `net8.0-windows`, `peak-sdk-csharp` core). _Deferred to the v0.2 adapter package:_ `KeychainSecureStorage` (iOS), `KeyStoreSecureStorage` (Android) | No; in v0.1.0 only on Windows (`net8.0-windows`) | Yes |
| Encrypted PlayerPrefs (interim) | `EncryptedPlayerPrefsStorage` — shipped by adapter packages for Unity-like runtimes, on top of the core `IStorage` abstraction; not part of this core package | No (explicit opt-in) | Yes |
| Unsafe plaintext | `UnsafePlaintextPlayerPrefsStorage` — **planned only, not implemented in any shipped package**; the opt-in guards below bind any future implementation | No | Yes |

`ISecureStorage` extends `IStorage`. `ISecureStorage.IsAvailable`
returns `false` when no platform-secure backend is wired (e.g. on a
generic Godot / console host on Linux or macOS, where the core SDK
ships no built-in implementation). Consumers MUST check `IsAvailable`
before persisting any High or Critical asset.

**Time-boxed exception (interim encrypted tier).** Until the adapter
packages gain OS-keystore-backed `ISecureStorage` providers, the
"Encrypted PlayerPrefs (interim)" tier is the accepted opt-in path for
persisting High / Critical assets on runtimes with no `ISecureStorage`
backend. It is NOT an `ISecureStorage`: its data-encryption key is
derived on-device in software (random seed file + device identifier,
HKDF-SHA256), not sealed in hardware. The exception expires when the
keystore providers ship; the interim tier then re-keys onto them.

### Interim tier: encrypted PlayerPrefs — residual risk

Values are AES-256-GCM encrypted before they reach PlayerPrefs, which
defeats the exposures that motivated this tier: OS backups of the
preferences store, forensic reads of the stored blob alone, and any
reader that obtains the ciphertext without the device. It does NOT
defeat an attacker with full file-system access on the same device
(rooted / jailbroken): the seed file and the device identifier are
both readable there, so the DEK can be reconstructed. Adapter
packages MUST document this limit, keep the seed out of OS backups
(no-backup location or flag; consumers exclude the preferences store
via backup rules), and treat plaintext residue from older versions as
exposed (delete, never migrate).

## v0.1.0 release blockers

- The default-out-of-the-box experience MUST persist nothing. A
  freshly-installed SDK with no consumer wiring of `IStorage` uses
  `InMemoryStorage` and forgets credentials on process exit. We accept
  the UX hit; resume-after-restart is the consumer's deliberate
  choice.
- `UnsafePlaintextPlayerPrefsStorage` is planned only — it is NOT
  implemented in any shipped package, and shipping a plaintext tier is
  currently not intended (the interim encrypted tier covers the
  persistence need). If it is ever implemented, it requires **both**
  of these to even be constructable:
  1. A compile-time symbol `PEAK_UNSAFE_STORAGE_OPT_IN` set on the
     **consumer's** csproj.
  2. A constructor argument `bool acknowledgePlaintext = true`. The
     parameter has no default; the constructor throws on `false`. The
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
| Unity iOS (IL2CPP) | `KeychainSecureStorage` (Unity adapter — deferred to v0.2) | `true` (planned) | `Security.framework` via P/Invoke; not in v0.1.0 |
| Unity Android (IL2CPP) | `KeyStoreSecureStorage` (Unity adapter — deferred to v0.2) | `true` (planned) | `AndroidKeyStore` via JNI; not in v0.1.0 |
| Unity standalone (Win/Mac/Linux) | Falls back to `DpapiSecureStorage` only on Windows; otherwise none | varies | Unity adapter respects the core matrix above |
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
| Disk forensics on a stolen machine reads PlayerPrefs | Default storage is `InMemoryStorage`; PlayerPrefs path requires explicit opt-in described above | Consumers who flip both opt-ins accept the risk explicitly |
| Malware on the same OS user reads app-data | OS-level secure store (DPAPI / Keychain / KeyStore) limits exposure to running as same user with biometrics | Consumer must wire `ISecureStorage`; not automatic |
| iCloud / Google Drive auto-backup snapshots app data | Default storage persists nothing. The interim encrypted tier keeps its key seed out of backups (no-backup directory on Android, do-not-back-up flag on iOS); the ciphertext in the preferences store is useless without the seed, and consumers MUST additionally exclude that store via platform backup rules (documented by the adapter packages) | A backup taken while the seed fell back to a backed-up location (no-backup dir unavailable) may contain seed + ciphertext; device binding is then the remaining barrier |
| OS-level keystore migration loses the key | OS-defined; for DPAPI the key is per-user and survives OS reinstall as long as `SID + password` are preserved | Consumers can re-trigger OTP login at any time |
| Cold-boot attack on RAM | None at this layer | Out of scope |
