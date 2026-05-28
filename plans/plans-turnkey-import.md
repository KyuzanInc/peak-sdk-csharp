# Plan — replace in-repo turnkey-sdk-csharp with external KyuzanInc.Turnkey.Sdk via GitHub Packages

## Intent

The Turnkey port now lives in
[KyuzanInc/turnkey-sdk-csharp](https://github.com/KyuzanInc/turnkey-sdk-csharp)
and ships as a NuGet package on GitHub Packages
(`https://nuget.pkg.github.com/KyuzanInc/index.json`). This repo
(`KyuzanInc/peak-sdk-csharp`) MUST stop building its own copy and
consume the published package instead. Net effect: drop the entire
`packages/turnkey-sdk-csharp/` subtree plus the `upstream-snapshots/`
mirrors and `codex-crypto-reviews/` evidence that supported it, fix
every CI and docs reference, add a single `PackageReference` to
`KyuzanInc.Turnkey.Sdk 0.1.0-alpha.0`, configure GitHub Packages auth,
and rename every `KyuzanInc.Turnkey.Sdk.*` qualifier in
`peak-sdk-csharp` to `Turnkey.*` (the namespace the external package
actually exports).

## Status overview

| ID | Workstream | Status |
|---|---|---|
| M1 | Investigate KyuzanInc/turnkey-sdk-csharp release + namespace + GitHub Packages location | ✅ Done |
| M2 | Remove in-repo `packages/turnkey-sdk-csharp/` (src + tests + READMEs); update `peak-sdk-csharp.sln` (drop Turnkey + Tests rows) | ⬜ Pending |
| M3 | Remove `upstream-snapshots/turnkey-sdk-unity/` and `upstream-snapshots/turnkey-official-src/` (if present); trim `upstream-snapshots/SOURCES.md` rows | ⬜ Pending |
| M4 | Remove `codex-crypto-reviews/` entirely (README, script, pins, evidence files) | ⬜ Pending |
| M5 | Remove `docs/security/crypto-port-policy.md` (in-repo crypto policy no longer applicable) | ⬜ Pending |
| M6 | Add `KyuzanInc.Turnkey.Sdk` `PackageReference` + GitHub Packages source in `nuget.config` + `--locked-mode` lock-file restore | ⬜ Pending |
| M7 | Rename `global::KyuzanInc.Turnkey.Sdk.*` → `global::Turnkey.*` in **every file under `packages/peak-sdk-csharp/src/**/*.cs` and `packages/peak-sdk-csharp/tests/**/*.cs`** | ⬜ Pending |
| M8 | Update **both** CI workflows (`csharp-ci.yml` AND `csharp-publish.yml`) for GitHub Packages auth + locked restore + drop dead `codex-review-evidence-check` job | ⬜ Pending |
| M9 | Update docs / config: `README.md`, `CLAUDE.md`, `docs/architecture.md`, `docs/development.md`, `docs/sync-rules.md`, `docs/security/storage-threat-model.md`, `.github/CODEOWNERS`, `Directory.Packages.props` (remove BouncyCastle pin), `packages/peak-sdk-csharp/README.md` and `README.public.md` | ⬜ Pending |
| M10 | Add a wire-format smoke test exercising every external method peak-sdk-csharp consumes + Peak wrapper serialization | ⬜ Pending |
| M11 | Build + `--locked-mode` restore + test green on all three runners; commit; push | ⬜ Pending |
| M12 | Codex review of plan; iterate until **no P1 findings remain**, THEN start M2 | ✅ Done — Codex round 2 returned "ALL 11 P1s ADDRESSED" on the revised plan |

## Background facts (from investigation)

- **Repo:** [`KyuzanInc/turnkey-sdk-csharp`](https://github.com/KyuzanInc/turnkey-sdk-csharp) (private, default branch `main`).
- **Release:** `v0.1.0-alpha.0` published 2026-05-26.
- **NuGet identity:** `<PackageId>KyuzanInc.Turnkey.Sdk</PackageId>`,
  `<AssemblyName>KyuzanInc.Turnkey.Sdk</AssemblyName>`,
  `<RootNamespace>Turnkey</RootNamespace>`.
- **TFMs:** `netstandard2.1;net8.0`.
- **Dependencies (transitive):** `BouncyCastle.Cryptography 2.5.0`,
  `System.Text.Json 8.0.5`.
- **Public surface (named by the external README):** `Turnkey.Crypto`,
  `Turnkey.Encoding`, `Turnkey.ApiKeyStamper`, `Turnkey.Http`. The
  external package also exports `Turnkey.CryptoConstants` (constants
  helper) but `Turnkey.TurnkeyJsonContext` is **internal** and is
  NOT part of the consumer surface. Nested DTOs we used from the
  in-repo port (`Http.SignedRequest`, `Http.InitImportPrivateKeyRequestBody`,
  `Http.InitImportPrivateKeyParameters`, `Http.ImportPrivateKeyRequestBody`,
  `Http.ImportPrivateKeyParameters`, `Http.ExportPrivateKeyRequestBody`,
  `Http.ExportPrivateKeyParameters`, `Http.ExportWalletAccountRequestBody`,
  `Http.ExportWalletAccountParameters`, `Http.Stamp`) MUST be verified
  against the actual `.nupkg` during M10; the in-repo plan assumes
  parity but the external package was built by a different process and
  may have minor surface drift.
- **Pinned upstream versions on the external package:**
  `@turnkey/crypto 2.8.8`, `@turnkey/http 3.16.0`,
  `@turnkey/api-key-stamper 0.5.0`, `@turnkey/encoding 0.6.0`. (The
  in-repo port had targeted 2.8.9 + 0.6.0; the external pin is the
  source of truth from now on. The drift question is captured as
  OQ-M2.)
- **Publish target:** GitHub Packages NuGet feed
  `https://nuget.pkg.github.com/KyuzanInc/index.json`, plus `.nupkg`
  + `.snupkg` assets uploaded to the GitHub Release.

## Decisions

| ID | Decision | Rationale |
|---|---|---|
| MD1 | Pin to **exact** `[0.1.0-alpha.0]` in `Directory.Packages.props`. CPM exact-range plus a `packages.lock.json` per project gives belt + suspenders. | A wallet SDK's Turnkey shim must never floating-bump. |
| MD2 | `nuget.config` adds a `github-kyuzan` source and uses `packageSourceMapping` so **only `KyuzanInc.Turnkey.*`** resolves through GitHub Packages. `nuget.org` continues to serve everything else (BouncyCastle, System.Text.Json, etc.). `local-feed` retained for downstream Unity adapter consumption and scoped to `KyuzanInc.Peak.*` (Codex P2 note: dropped any reference to `PR 5` numbering). | Two narrow private feeds, neither shadowing nuget.org. |
| MD3 | CI auth: **explicit `dotnet nuget update source github-kyuzan` step** (not `add source` — the repo's nuget.config already declares the source name, and `add source` would error with duplicate-source) with `--username ${{ github.actor }} --password "$GITHUB_TOKEN"`. We do NOT rely on `actions/setup-dotnet@v4`'s `nuget-auth-token` (that input does not exist on v4 — `setup-dotnet@v3` had `source-url`/`NUGET_AUTH_TOKEN` semantics but v4 dropped per-input auth; the explicit update-source step is the only stable pattern). Workflow `permissions:` MUST grow `packages: read`. Cross-repo read access for `GITHUB_TOKEN` on the package is a separate prerequisite (see OQ-M4). | Stable, debuggable; works on every runner. |
| MD4 | Adopt `Turnkey` namespace in peak-sdk-csharp consumers. The rename target is **every file under `packages/peak-sdk-csharp/src/**/*.cs` AND `packages/peak-sdk-csharp/tests/**/*.cs`** (currently 4 source-side files reference the in-repo namespace: Models.cs, AuthService.cs, PrivateKeyService.cs, SessionJwt.cs; the tests do not reference it directly today but the rename pass must walk both trees so no future regression slips by). | Mechanical search-and-replace `global::KyuzanInc.Turnkey.Sdk.` → `global::Turnkey.` plus the PeakJsonContext `[JsonSerializable]` attributes. |
| MD5 | Delete `codex-crypto-reviews/`, `upstream-snapshots/turnkey-sdk-unity/`, `upstream-snapshots/turnkey-official-src/` (if present), and `docs/security/crypto-port-policy.md`. | Crypto port + review evidence now lives in `KyuzanInc/turnkey-sdk-csharp`. Stale copies invite drift. |
| MD6 | Keep `upstream-snapshots/peak-sdk-unity/`. | Still the port base for `KyuzanInc.Peak.Sdk`. |
| MD7 | Drop the dead `codex-review-evidence-check` job from `csharp-ci.yml`. The CI job is currently the only validation that the crypto evidence files were updated when crypto code changed; with crypto out of repo, the gate becomes a vacuous pass and is misleading dead code. | The corresponding gate lives in `KyuzanInc/turnkey-sdk-csharp` from now on. |
| MD8 | Lock-file restore: enable `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` on **both** `packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj` AND `packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj`. Commit both `packages.lock.json` files. CI restores with `--locked-mode`. | One project under lock + one without would split the truth between them. |
| MD9 | Remove `BouncyCastle.Cryptography` pin from `Directory.Packages.props`. peak-sdk-csharp never references it directly; it now arrives transitively through `KyuzanInc.Turnkey.Sdk`. | CPM transitive pinning will still freeze the BouncyCastle version from the lock files; an explicit pin in our props is dead weight that has to be kept in sync with the external repo. |
| MD10 | Update **both** CI workflows. `csharp-ci.yml` for build + locked restore + tests on Linux/macOS/Windows runners. `csharp-publish.yml` for the publish job which also restores + packs and therefore needs the same GitHub Packages auth + locked restore. | A previous draft of this plan said the publish workflow was untouched. That was wrong — `dotnet pack` triggers a transitive restore. |
| MD11 | Verification adds a wire-format smoke test (M10) that compiles against the external `KyuzanInc.Turnkey.Sdk 0.1.0-alpha.0` and asserts the actual byte-level JSON shape produced by `Http.StampInitImportPrivateKey`, `Http.StampImportPrivateKey`, `Http.StampExportPrivateKey`, `Http.StampExportWalletAccount`, `Http.StampGetWhoami`, plus `Crypto.GenerateP256KeyPair()` and `Crypto.VerifySessionJwtSignature(...)`. It also exercises `PeakJsonContext`-driven serialization of `InitImportPrivateKeyRequest`, `CompleteImportPrivateKeyRequest`, `ExportPrivateKeyRequest` (the Peak wrappers around the Turnkey `SignedRequest`) and asserts the outer JSON key names. | Codex r1 P1: surface assumption needs proof from the actual `.nupkg`. |

## File-by-file change plan

### Removed (M2-M5)

```
packages/turnkey-sdk-csharp/                        DELETE entire subtree
  src/{turnkey-sdk-csharp.csproj,ApiKeyStamper.cs,Crypto.cs,CryptoConstants.cs,
       Encoding.cs,Http.cs,TurnkeyJsonContext.cs}
  tests/{turnkey-sdk-csharp.Tests.csproj,ApiKeyStamperTests.cs,CryptoTests.cs,
         EncodingTests.cs,HttpTests.cs,Fixtures/}
  README.md, README.public.md

upstream-snapshots/turnkey-sdk-unity/               DELETE
upstream-snapshots/turnkey-official-src/            DELETE if present

codex-crypto-reviews/                               DELETE entire subtree
  README.md, codex-crypto-review.sh
  turnkey-source-pins.md, unity-source-pins.md
  Crypto.cs-r1-2026-05-27.md
  full-impl-final-2026-05-27.md

docs/security/crypto-port-policy.md                 DELETE
```

### Edited (M6-M9, M11)

```
peak-sdk-csharp.sln                                 remove KyuzanInc.Turnkey.Sdk + KyuzanInc.Turnkey.Sdk.Tests project rows
                                                     (Project GUIDs A0000001-...-0001 and ...-0002 today)

Directory.Packages.props                            ADD: <PackageVersion Include="KyuzanInc.Turnkey.Sdk" Version="[0.1.0-alpha.0]" />
                                                     REMOVE: <PackageVersion Include="BouncyCastle.Cryptography" Version="[2.5.0]" />
                                                     KEEP: System.Text.Json, Microsoft.Extensions.Logging.Abstractions,
                                                            Microsoft.Extensions.Http.Polly, Microsoft.Extensions.Http,
                                                            System.Security.Cryptography.ProtectedData, test deps

nuget.config                                        ADD source `github-kyuzan` = https://nuget.pkg.github.com/KyuzanInc/index.json
                                                     ADD packageSourceMapping:
                                                       nuget.org      -> *
                                                       github-kyuzan  -> KyuzanInc.Turnkey.*
                                                       local-feed     -> KyuzanInc.Peak.*

upstream-snapshots/SOURCES.md                       REMOVE turnkey-sdk-unity row
                                                     REMOVE turnkey-official-src row
                                                     KEEP peak-sdk-unity row

packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj DROP <ProjectReference Include="..\..\turnkey-sdk-csharp\src\turnkey-sdk-csharp.csproj" />
                                                     ADD  <PackageReference Include="KyuzanInc.Turnkey.Sdk" />
                                                     ADD  <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
                                                     KEEP <EnableWindowsTargeting>true</EnableWindowsTargeting>

packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj
                                                     ADD  <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>

packages/peak-sdk-csharp/src/**/*.cs                rename `global::KyuzanInc.Turnkey.Sdk.` → `global::Turnkey.`
                                                     (sed pass; specific files known to need it today:
                                                      Models/Models.cs, Services/AuthService.cs,
                                                      Services/PrivateKeyService.cs, Utils/SessionJwt.cs)

packages/peak-sdk-csharp/tests/**/*.cs              same pass (defensive; no current matches but block future regression)

packages/peak-sdk-csharp/src/PeakJsonContext.cs     [JsonSerializable] type qualifiers and registrations:
                                                     no direct Turnkey refs today (the in-repo PeakJsonContext
                                                     only registers Peak DTOs), so this file likely needs no
                                                     change. Confirmed by `grep KyuzanInc.Turnkey` on the file.

.github/workflows/csharp-ci.yml
  - permissions: contents: read, packages: read           (grow)
  - REMOVE codex-review-evidence-check job
  - Before every restore step, attach credentials to the
    `github-kyuzan` source the repo's nuget.config already declares
    (use update-source, not add-source — adding a duplicate source
    name errors). Drop --source: the URL is already in nuget.config:
      env:
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      run: |
        dotnet nuget update source github-kyuzan \
          --username "${{ github.actor }}" \
          --password "$GITHUB_TOKEN" \
          --store-password-in-clear-text
        dotnet restore peak-sdk-csharp.sln
        # NOTE: --locked-mode lands in the M11 follow-up once
        # packages.lock.json files are committed.
  - consumer-restore-check: same auth setup before the consumer
    project's `dotnet restore`. Without it, the consumer fails to
    pull KyuzanInc.Turnkey.Sdk transitively.

.github/workflows/csharp-publish.yml
  - permissions: contents: read, packages: read   (publish step already had packages: write)
  - same `dotnet nuget update source github-kyuzan` step before
    restore/build/pack/test
  - plain restore for now; --locked-mode lands with the lock files
    in the M11 follow-up

.github/CODEOWNERS                                  REMOVE rows referring to
                                                     /packages/turnkey-sdk-csharp/src/*.cs and
                                                     /codex-crypto-reviews/.
                                                     KEEP /docs/security/ and
                                                     /upstream-snapshots/peak-sdk-unity rows.

README.md                                           REWRITE "Packages" section: the turnkey row goes; add a
                                                     "Dependencies" subsection that lists
                                                     KyuzanInc.Turnkey.Sdk as an external dependency with a
                                                     link to KyuzanInc/turnkey-sdk-csharp.

CLAUDE.md                                           DELETE "Cryptographic code" section (3 paragraph block).
                                                     REPLACE with: "Crypto lives in KyuzanInc/turnkey-sdk-csharp.
                                                     Open a PR there for any crypto change; the multi-round
                                                     Codex review evidence is gated in that repo's CI."
                                                     Remove the bullet about "Newtonsoft.Json forbidden" (still
                                                     true but no longer this repo's enforcement scope).

docs/architecture.md                                Layering diagram: turnkey-sdk-csharp row becomes a
                                                     third-party package, not a hosted package. TFM table
                                                     loses the turnkey row.

docs/development.md                                 NEW section "GitHub Packages auth setup":
                                                       - local: `dotnet nuget add source ... --password "$GITHUB_TOKEN"`
                                                       - or `~/.nuget/NuGet/NuGet.Config` snippet
                                                     Add: `dotnet restore --locked-mode` is mandatory for
                                                     reproducibility.

docs/sync-rules.md                                  REMOVE the `tkhq-sdk` resync row (no longer this repo).
                                                     ADD a new row "Bump KyuzanInc.Turnkey.Sdk":
                                                       1. edit Directory.Packages.props version
                                                       2. dotnet restore --force-evaluate
                                                       3. commit packages.lock.json files
                                                       4. run M10 wire-format smoke against the new version
                                                       5. open PR

docs/security/storage-threat-model.md               No content change required; the file does NOT reference
                                                     crypto-port-policy.md by link. Confirmed by `grep
                                                     crypto-port-policy docs/security/storage-threat-model.md`.

packages/peak-sdk-csharp/README.md                  Remove the "Crypto-port-policy: D17-CLAR..." paragraph
                                                     and any pointer to the in-repo turnkey package.

packages/peak-sdk-csharp/README.public.md           ADD a "Consumer setup: GitHub Packages auth required"
                                                     paragraph above "Install". List the exact commands and
                                                     link to docs/development.md.

plans/plans-peak-sdk-csharp.md                      Amend the status table: W0b row -> "external Turnkey
                                                     package consumed (KyuzanInc.Turnkey.Sdk)"; W1 row ->
                                                     "out of scope here, lives in KyuzanInc/turnkey-sdk-csharp".

plans/plans-turnkey-import.md                       this file.
```

### Added (M6, M10)

```
packages/peak-sdk-csharp/src/packages.lock.json     generated by `dotnet restore` after M6
packages/peak-sdk-csharp/tests/packages.lock.json   generated by `dotnet restore` after M6

packages/peak-sdk-csharp/tests/TurnkeyWireFormatSmokeTests.cs
                                                     M10 deliverable. Compiles against the external package
                                                     and exercises (each assertion ties to a peak-sdk-csharp
                                                     consumption site):
                                                     - Crypto.GenerateP256KeyPair() returns 32-byte
                                                       PublicKey/PrivateKey hex
                                                     - Crypto.VerifySessionJwtSignature("malformed") is false
                                                     - Http.FromTargetPrivateKey(testHex) constructs without throw
                                                     - StampGetWhoami body: organizationId field
                                                     - StampInitImportPrivateKey body: organizationId, type,
                                                       timestampMs, parameters.userId
                                                     - StampImportPrivateKey body: parameters.userId,
                                                       parameters.addressFormats, parameters.curve,
                                                       parameters.encryptedBundle, parameters.privateKeyName
                                                     - StampExportPrivateKey body: parameters.privateKeyId,
                                                       parameters.targetPublicKey
                                                     - StampExportWalletAccount body: parameters.address,
                                                       parameters.targetPublicKey
                                                     - All SignedRequest results: Url, Body, Stamp,
                                                       Stamp.StampHeaderName == "X-Stamp"
                                                     - PeakJsonContext serializes
                                                       InitImportPrivateKeyRequest with outer key
                                                       `signedInitImportPrivateKeyRequest`
                                                     - Similar coverage for CompleteImportPrivateKeyRequest,
                                                       ExportPrivateKeyRequest

docs/runbook-bump-turnkey-sdk.md (optional)         How-to for maintainers bumping the external pin.
```

## Verification

After M2-M11:

**Local (dev machine):**

```bash
# One-time GitHub Packages auth. The user-level NuGet config is
# independent of the repo's, so on a clean machine you first run
# `add source` (so the user config knows the source name) and then
# use `update source` for later token refreshes. `${{ github.actor }}`
# is GitHub Actions-only — substitute your own GitHub username.
export GITHUB_TOKEN=ghp_...   # PAT with read:packages scope

# First time on this machine — declare the source in the user config:
dotnet nuget add source https://nuget.pkg.github.com/KyuzanInc/index.json \
  --name github-kyuzan \
  --username <your-github-username> \
  --password "$GITHUB_TOKEN" \
  --store-password-in-clear-text \
  --configfile ~/.nuget/NuGet/NuGet.Config

# Subsequent token refreshes use update-source instead of re-adding:
# dotnet nuget update source github-kyuzan \
#   --username <your-github-username> \
#   --password "$GITHUB_TOKEN" \
#   --store-password-in-clear-text \
#   --configfile ~/.nuget/NuGet/NuGet.Config

cd ~/Kyuzan/src/peak-sdk-csharp
dotnet restore peak-sdk-csharp.sln                       # MUST be green; --locked-mode lands in the M11 follow-up
dotnet build  peak-sdk-csharp.sln -c Release             # MUST be green (netstandard2.1 + net8.0 + net8.0-windows)
dotnet test   peak-sdk-csharp.sln -c Release \
              --filter "Category!=E2E"                   # 22 existing peak tests + N new wire-format smoke tests
```

**CI matrix (`csharp-ci.yml`, ubuntu + macos + windows):**

- `dotnet nuget update source github-kyuzan` step succeeds (proves
  `secrets.GITHUB_TOKEN` has `packages: read` permission via the
  workflow grant AND that the package owner repo has granted this
  repo read access on the published nupkg — see OQ-M4).
- `dotnet restore peak-sdk-csharp.sln` succeeds. Add `--locked-mode`
  after the M11 follow-up commits the `packages.lock.json` files; the
  current bootstrap intentionally runs without it.
- `dotnet build` succeeds.
- `dotnet test --filter Category!=E2E` succeeds, including the new
  wire-format smoke.
- `consumer-restore-check` job (now using the same auth setup)
  succeeds for both `netstandard2.1` and `net8.0`.

**Publish workflow dry-run (`csharp-publish.yml`):**

- Trigger via `workflow_dispatch`, NOT by pushing a `v*` tag (no
  publish on a dry-run).
- Same auth + locked restore + pack succeeds. Pack artefact appears
  under `./artifacts/` as `KyuzanInc.Peak.Sdk.<version>.nupkg`.

## Risks + mitigation

| Risk | Likelihood | Mitigation |
|---|---|---|
| External package surface differs subtly from our in-repo port (different property casing, missing nested DTO, etc.) | medium | M10 wire-format smoke compiles + asserts every field name. Codex r2 also reviews after the smoke lands. |
| CI's `GITHUB_TOKEN` lacks cross-repo `read:packages` access on `KyuzanInc/turnkey-sdk-csharp` | high | OQ-M4. Verified by ssh / manual restore on macOS dev machine before opening the PR; CI failure surfaces it loudly with a "401 Unauthorized" body. |
| Transitive downstream consumers of `KyuzanInc.Peak.Sdk` discover they now need GitHub Packages auth too | high (fundamental) | Loud disclosure in `README.public.md` "Consumer setup" section. Long-term fix is publishing `KyuzanInc.Turnkey.Sdk` to nuget.org (OQ-M1). |
| `packages.lock.json` drifts after every GitHub Packages prerelease | low | `--locked-mode` fails CI; the bump is a deliberate PR per `docs/sync-rules.md`. |
| `actions/setup-dotnet@v4` someday adds a `nuget-auth-token` input and our explicit step becomes redundant | low | Cheap to migrate later. |
| Removing `BouncyCastle.Cryptography` from CPM breaks something we hadn't noticed | low | Solution-wide search `dotnet list package --include-transitive` confirms BouncyCastle still arrives via `KyuzanInc.Turnkey.Sdk`. If a peak-sdk-csharp source file references BouncyCastle directly, the build will fail and we re-add the pin. |

## Open questions

| # | Question | Owner | Status |
|---|---|---|---|
| OQ-M1 | When does `KyuzanInc.Turnkey.Sdk` move to nuget.org? Until then every downstream consumer of `KyuzanInc.Peak.Sdk` needs GitHub Packages auth. | Komy CTO | Open. Documented loudly in `README.public.md`. |
| OQ-M2 | The external package pins `@turnkey/crypto 2.8.8` while our in-repo port targeted 2.8.9. Bump the external pin or accept the drift? | Komy CTO | Open. File an issue on `KyuzanInc/turnkey-sdk-csharp` if peak-sdk-browser depends on a 2.8.9-only behaviour. |
| OQ-M3 | Should the migration commit bump `KyuzanInc.Peak.Sdk` to `0.1.0-alpha.1`? The dependency change is breaking for any external consumer (now needs GitHub Packages auth). | TJ | Open. Default: yes. |
| OQ-M4 | Does the `KyuzanInc/peak-sdk-csharp` repo's `GITHUB_TOKEN` have `read:packages` on `KyuzanInc/turnkey-sdk-csharp`'s published package? Cross-repo private package access is opt-in via the package's "Manage access" UI. | TJ | Open. Verify before M11 runs; if not, request access from `KyuzanInc/turnkey-sdk-csharp` maintainer. |

## What stays as-is

- `KyuzanInc.Peak.Sdk` public surface (PeakClient, AuthenticatedPeakClient, services, PeakError, IStorage, DpapiSecureStorage). Only the way it gets `Turnkey.*` types changes.
- `KyuzanInc.Peak.Sdk` TFM stack (netstandard2.1 + net8.0 + net8.0-windows).
- `upstream-snapshots/peak-sdk-unity/` (port base, unchanged).
- `docs/security/storage-threat-model.md` and `docs/security/secure-storage-platform-matrix.md`.
- `.github/workflows/csharp-publish.yml` publish job (just gains the same nuget-add-source step the rest of CI does).

## GSTACK REVIEW REPORT

| Review | Trigger | Why | Runs | Status | Findings |
|--------|---------|-----|------|--------|----------|
| Codex Review | `/codex` consult | Independent 2nd opinion before deletion | 2 | CLEAR after round 2 — Codex confirmed "ALL 11 P1s ADDRESSED" on the revised plan (`/tmp/codex-r2-tight-resp.jsonl`, tokens in=58116 out=472, 2026-05-27) | round 1 raised 11 P1 + 6 P2; round 2 confirms all P1 fixed |
| Eng Review | `/plan-eng-review` | Architecture | 0 | Skipped — mechanical refactor, no architecture change |
| CEO Review | `/plan-ceo-review` | Strategy | 0 | Skipped — user directive locks scope |
| Design Review | `/plan-design-review` | UI/UX | N/A | library, no UI |
| DX Review | `/plan-devex-review` | Developer experience | 0 | Optional — GitHub Packages PAT setup is a DX hit worth a careful look later (OQ-M1) |

**CODEX:** Round 1 (commit `c5ea813`) returned 11 P1 + 6 P2 findings; this revision addresses every one in MD3, MD4, MD7-MD11, the file-by-file table, the M10 wire-format smoke, and the OQ-M1..M4 list. Round 2 (run against the revised plan) returned "ALL 11 P1s ADDRESSED" — verdict P1-clean.
**UNRESOLVED:** OQ-M1 (nuget.org publish), OQ-M2 (crypto 2.8.8 vs 2.8.9 pin drift), OQ-M3 (peak-sdk version bump), OQ-M4 (cross-repo GitHub Packages read access).
**VERDICT:** CLEARED — Codex round 2 returned P1-clean on the revised plan. Implementation (M2 onward) may proceed.
