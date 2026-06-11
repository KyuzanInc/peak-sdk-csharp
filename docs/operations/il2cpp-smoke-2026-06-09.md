# IL2CPP smoke — peak-sdk-unity-reference

- **Date:** 2026-06-09 (design); Editor CLI verification 2026-06-11; IL2CPP Player run: (fill the actual run date)
- **Runner:** Editor CLI verification automated; Player run: (name)
- **Unity version:** 6000.0.73f1 (a166abc3bf0e)
- **NuGetForUnity:** v4.4.0
- **SDK:** KyuzanInc.Peak.Sdk 0.1.0-alpha.2 (local feed) + KyuzanInc.Turnkey.Sdk 0.1.0-alpha.0 (vendored)

## Configuration
- Scripting backend: IL2CPP (committed in ProjectSettings)
- Api Compatibility Level: .NET Standard 2.1
- Managed stripping level: **Low** (committed default; a **Disabled** build strips nothing, so a Disabled pass is NOT a valid smoke result — the level must be Low or higher to actually exercise AOT stripping)
- Target platform(s): Standalone (mac/win/linux) [; iOS / Android if toolchains available]
- link.xml present: yes (Assets/link.xml — preserves BouncyCastle, Turnkey, Peak.Sdk, System.Text.Json)

## Editor CLI verification (2026-06-11, automated — `Unity -batchmode`)
Ran Unity 6000.0.73f1 headless against the project. Confirmed:
- **Project opens with zero errors** and NuGetForUnity (v4.4.0, git-URL UPM) resolves + compiles.
- **NuGetForUnity restored the full 21-package closure** from `Assets/NuGet.config`'s
  `../LocalFeed` (+ nuget.org) into `Assets/Packages/` — incl. `KyuzanInc.Peak.Sdk`,
  `KyuzanInc.Turnkey.Sdk`, `BouncyCastle.Cryptography`, `System.Text.Json`, the
  `Microsoft.Extensions.*` set, and `System.Numerics.Vectors`.
- **No-GitHub-auth path proven:** re-ran the restore with the machine's *global*
  NuGet config temporarily hidden (so no `github-kyuzan` source is injected). All 21
  packages — incl. `KyuzanInc.Peak.Sdk` + `KyuzanInc.Turnkey.Sdk` — still restored,
  from the local feed alone. This confirms the example needs no GitHub Packages auth.
- **`PeakExampleDemo.cs` compiles clean** against the restored SDK (`Peak.Example.dll`
  built, **0 compile errors**). The `Peak.Example` asmdef picks up the restored DLLs
  via NuGetForUnity's default Auto-Reference (no `overrideReferences` needed).

This covers the **Editor/Mono compile** path (the project opens, restores, and the
script compiles against the real SDK surface). The IL2CPP **Player build** + the
runtime OTP/wallet flow still need a human run (Standalone IL2CPP build + a Peak
test environment + test `projectApiKey`/keys).

## Results (per step)
| Step | Editor (CLI) | IL2CPP Player | Notes |
|---|---|---|---|
| Open project (zero errors) | ✅ 6000.0.73f1 | n/a | |
| NuGetForUnity restore (Assets/Packages populated) | ✅ 21 pkgs from LocalFeed + nuget.org | n/a | |
| Scripts compile (Peak.Example.dll, 0 errors) | ✅ | n/a | Editor/Mono compile |
| Initialize | ☐ | ☐ | needs runtime |
| Send OTP (InitOtpLoginAsync) | ☐ | ☐ | HttpClient + STJ |
| Complete Login (CompleteOtpLoginAsync) + Authenticate | ☐ | ☐ | async state machine |
| List Accounts / Addresses | ☐ | ☐ | STJ source-gen deserialize |
| Import (PeakCrypto.Encrypt + CompleteImport, chainType="evm") | ☐ | ☐ | BouncyCastle under AOT |
| Export (GenerateP256KeyPair + Export + DecryptExportBundle) | ☐ | ☐ | BouncyCastle under AOT |
| Post-await main-thread id == captured main-thread id | ☐ | ☐ | ConfigureAwait/SynchronizationContext (D7) |

## link.xml / stripping findings
- (Editor compile needs no link.xml. The link.xml matters at IL2CPP Player build:
  did any assembly need preserving that the seed link.xml missed? Could any entry be
  removed? Did a stripping level break BouncyCastle? Record here — a stripping
  failure is a SUCCESSFUL finding.)

## Outcome
- **PARTIAL** — Editor open + NuGetForUnity restore + script compile verified via
  CLI in Unity 6000.0.73f1 (0 errors). The IL2CPP Standalone Player build + the
  end-to-end OTP/wallet runtime flow remain to be run by a human (needs a Player
  build + Peak test credentials).
