# IL2CPP smoke — peak-sdk-unity-reference

- **Date:** 2026-06-09 (fill the actual run date)
- **Runner:** (name)
- **Unity version:** 6000.0.____ (from ProjectSettings/ProjectVersion.txt)
- **NuGetForUnity:** v____ (from Packages/manifest.json)
- **SDK:** KyuzanInc.Peak.Sdk 0.1.0-alpha.2 (local feed) + KyuzanInc.Turnkey.Sdk 0.1.0-alpha.0 (vendored)

## Configuration
- Scripting backend: IL2CPP
- Api Compatibility Level: .NET Standard 2.1
- Managed stripping level: ____ (committed default = **Low**; record what was tested. A **Disabled** build strips nothing, so a Disabled pass is NOT a valid smoke result — the level must be Low or higher to actually exercise AOT stripping.)
- Target platform(s): Standalone (mac/win/linux) [; iOS / Android if toolchains available]
- link.xml present: yes (Assets/link.xml — preserves BouncyCastle, Turnkey, Peak.Sdk, System.Text.Json)

## Results (per step)
| Step | Editor Play | IL2CPP Player | Notes |
|---|---|---|---|
| Open project (zero errors) | ☐ | n/a | |
| NuGetForUnity restore (Assets/Packages populated) | ☐ | n/a | |
| Initialize | ☐ | ☐ | |
| Send OTP (InitOtpLoginAsync) | ☐ | ☐ | HttpClient + STJ |
| Complete Login (CompleteOtpLoginAsync) + Authenticate | ☐ | ☐ | async state machine |
| List Accounts / Addresses | ☐ | ☐ | STJ source-gen deserialize |
| Import (PeakCrypto.Encrypt + CompleteImport, chainType="evm") | ☐ | ☐ | BouncyCastle under AOT |
| Export (GenerateP256KeyPair + Export + DecryptExportBundle) | ☐ | ☐ | BouncyCastle under AOT |
| Post-await main-thread id == captured main-thread id | ☐ | ☐ | ConfigureAwait/SynchronizationContext (D7) |

## link.xml / stripping findings
- (Did any assembly need preserving that the seed link.xml missed? Could any entry be removed? Did a stripping level break BouncyCastle? Record here — a stripping failure is a SUCCESSFUL finding.)

## Outcome
- PASS / FAIL / PARTIAL — (summary)
