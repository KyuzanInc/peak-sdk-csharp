# peak-sdk-csharp port plan (single-repo adapted)

> **Historical scope notice — read before relying on details below.**
>
> The original plan called for `KyuzanInc.Turnkey.Sdk` and its review
> evidence to live in this repo. That work was extracted to
> [`KyuzanInc/turnkey-sdk-csharp`](https://github.com/KyuzanInc/turnkey-sdk-csharp)
> per [`plans/plans-turnkey-import.md`](plans-turnkey-import.md). As a
> result, **any reference in this document to**:
>
> - `packages/turnkey-sdk-csharp/` (deleted)
> - `upstream-snapshots/turnkey-sdk-unity/` (deleted)
> - `upstream-snapshots/turnkey-official-src/` (never created in this repo)
> - `codex-crypto-reviews/` (deleted)
> - `docs/security/crypto-port-policy.md` (deleted)
> - CODEOWNERS rules for `/packages/turnkey-sdk-csharp/src/Crypto.cs`
>
> is **historical**. Crypto code, its upstream pins, its multi-round
> Codex review evidence, and the CODEOWNERS gate for those files all
> live in `KyuzanInc/turnkey-sdk-csharp`. The consumer-side flow in
> this repo is documented in [`docs/sync-rules.md`](../docs/sync-rules.md).
>
> Sections below describe the repo as it was when those paths existed.
> Do not follow them for new work; they are kept for context on how
> the v0.1.0-alpha state was assembled.

## Intent

Port `peak-sdk-unity` + `turnkey-sdk-unity` from Unity-only C# to generic
.NET NuGet packages, hosted in this **standalone repository** at
`KyuzanInc/peak-sdk-csharp` (private until v0.1.0 ships). Four packages
under `packages/`:

- `KyuzanInc.Turnkey.Sdk` — crypto / API key stamping (port of
  `@turnkey/{crypto,api-key-stamper,http}@2.8.9/0.6.0/3.16.0`)
- `KyuzanInc.Peak.Sdk` — Peak SDK + `IStorage` / `ISecureStorage`
- `KyuzanInc.Peak.PublicApiClient` — internal OpenAPI codegen client
- `KyuzanInc.Peak.Sdk.Unity` — Unity-only platform adapter

The original `KyuzanInc/peak` monorepo is left untouched. OpenAPI specs
and Unity source code are mirrored into `upstream-snapshots/` at pinned
commits.

## Status overview

| ID | Workstream | Status | Evidence | Commit | Finding |
|---|---|---|---|---|---|
| W0a | Repo bootstrap (root sln, shared props, CPM, CI workflow, docs scaffold) | ✅ Done | `e712d18`, `4503397`, `d105f7a` | initial commits | new-repo adjustment |
| W0b | External Turnkey package consumed (KyuzanInc.Turnkey.Sdk via GitHub Packages); peak-sdk-unity @ fc560e8 mirror retained | ✅ Done | `4503397` + `upstream-snapshots/SOURCES.md` + `plans/plans-turnkey-import.md` | `4503397` | Codex D-P1 |
| W0c | OpenAPI sync workflow + drift CI (snapshot tracks peak-server `main`) | ✅ Done | `scripts/generate-public-api-client.sh` + `openapi-client-drift` CI job; internal `KyuzanInc.Peak.PublicApiClient` generated from `peak-server` `main` HEAD (recorded as a commit SHA per sync). Consumed by the SDK as of PR #14 (`a010bbe`): generated response DTOs are deserialized internally and mapped to the public surface in `src/Mapping/` and the services, never exposed publicly. | #12 (`6196b0e`), SDK wiring #14 (`a010bbe`) | Codex D-P1 |
| W0d | Unity `link.xml`: preserve `Newtonsoft.Json` and the generated `KyuzanInc.Peak.PublicApiClient.Model` namespace in `peak-sdk-csharp-unity` so IL2CPP stripping keeps them | ⬜ Follow-up | spec R1/OQ2 | — | consume-generated-openapi-client |
| W0e | Slim the generated client to models-only once a generator-core bump can emit dependency-light DTOs, dropping RestSharp/Polly; also revisit request-DTO adoption | ⬜ Follow-up | spec R2/OQ3 | — | consume-generated-openapi-client |
| W1  | PR 1: turnkey crypto / API key stamping | ⬜ Out of scope here — lives in `KyuzanInc/turnkey-sdk-csharp` (consumed as `KyuzanInc.Turnkey.Sdk 0.1.0-alpha.0`) | external repo `KyuzanInc/turnkey-sdk-csharp` v0.1.0-alpha.0 release | — | per `plans/plans-turnkey-import.md` |
| W2  | PR 2: peak-sdk-csharp PeakClient + services + IStorage + PeakError + Codex orgId fix | ✅ Done | 22/22 tests pass | `afc3dc8` | — |
| W3  | PR 3: Godot + console smoke examples + Unity reference example via local file feed | ⬜ Deferred to v0.1.0-alpha.1 | scope decision | — | — |
| W4  | PR 4: remaining v0.1.0 API surface (Account × 3 + PrivateKey × 3 + internal × 1) | ✅ Done (folded into W2) | AccountService + PrivateKeyService landed in PR 2 | `afc3dc8` | — |
| W4.5| PR 4.5: SecureStorage core (Windows DPAPI + UnavailableSecureStorage placeholder) | ✅ Done | `docs/security/secure-storage-platform-matrix.md` | `6847839` | Codex C4 |
| W5  | PR 5 (deferred to separate repo): turn `peak-sdk-unity` into thin adapter | ⬜ Out of scope here | external repo work | — | per user directive |
| W6  | (skipped) peak monorepo submodule bump | ⛔ N/A | per user "do not update peak repo" | n/a | — |
| W7  | PR 7: NuGet.org publish workflow + public docs scaffolding | ✅ Done | `.github/workflows/csharp-publish.yml` | `6847839` | — |
| Wc1 | Crypto fuzz / property tests (front-loaded from Phase 2) | ⬜ Backlog | scope decision; covered partially by `CryptoTests.cs` | — | Codex C-P1 |
| Wc2 | Cross-SDK conformance suite (TS family equivalence) | ⬜ Backlog | scope decision | — | Codex E-P2 |
| WrF | Final Codex review of full implementation | 🚧 In progress | running at commit `6847839` | — | FINISH CONDITION |

## Codex P1 findings adjudication (review session 2026-05-27)

Source: independent Codex review of original `KyuzanInc/peak`
`plans/plans-peak-sdk-csharp.md`, against the Unity port sources at the
commits captured in `upstream-snapshots/SOURCES.md`.

| # | Finding | Verdict | Reflected in |
|---|---|---|---|
| C-A1 | D17 (1:1 port) and PR 1 Goal (D10 wrapper) contradict each other | ✅ Accepted | D17-CLAR below; PR 1 Goal text rewritten |
| C-A2 | Test fixtures too shallow; HpkeEncrypt round-trip cannot catch ephemeral / AAD / KEM context drift | ✅ Accepted | PR 1 fixture matrix expanded; Wc1 added |
| C-B1 | `UnsafePlaintextPlayerPrefsStorage` rename alone is not enough for wallet SDK | ✅ Accepted | PR 4.5 adds compile symbol + per-instance ack flag + release blocker checklist |
| C-B2 | Core `ISecureStorage` only Windows DPAPI leaves Godot/Linux/macOS unprotected | ✅ Accepted | `ISecureStorage.IsAvailable` contract; non-Windows platforms throw or return false |
| C-C1 | "Codex multi-round = paid-audit substitute" is dangerous wording | ✅ Accepted | D17-CLAR rewritten: Codex is **review evidence**, not security assurance. R15 severity raised to High. Paid audit re-confirmed as v1.0.0 blocker |
| C-D1 | Plan still assumes peak-monorepo path layout (per-package CPM, pnpm wrapper, RepositoryUrl) | ✅ Accepted | D19-CLAR: in single-repo we use **root** `peak-sdk-csharp.sln` + root `Directory.Build.props` + root `Directory.Packages.props`. Per-package isolation reverted |
| C-D2 | OpenAPI source-of-truth disconnected from new repo | ✅ Accepted | W0c added (sync workflow), `upstream-snapshots/peak-server-openapi/` mirrors a pinned tag |
| C-E1 | Premise 5 over-claims TS family semantic equivalence | ✅ Accepted | Premise 5 rewritten to "Unity API semantic port + selected TS behavior in v0.1.0" |
| C-E2 | IL2CPP smoke depends on `peak-sdk-unity-example` which is not in this repo | ✅ Accepted | PR 3 hosts a minimal Unity-shaped example; or pulls peak-sdk-unity as a worktree fixture during CI |

## Clarified decisions (delta vs original D1-D20)

The original D1-D20 from the monorepo plan still hold **except** the
following:

- **D17-CLAR (replaces D17)** — Codex multi-round review (≥3 per crypto
  file) is **review evidence**, not a security audit. Since the
  `plans/plans-turnkey-import.md` migration, crypto code lives in
  `KyuzanInc/turnkey-sdk-csharp` and the per-file review evidence is
  committed there (not in this repo). A paid third-party audit of the
  Turnkey crypto port remains a v1.0.0 release blocker. R15 severity is
  **High**.
- **D19-CLAR (reverses D19)** — In this single-repo, csharp is the only
  thing in the repo, so the "keep csharp out of the monorepo root"
  motivation does not apply. We use a single root `peak-sdk-csharp.sln`,
  root `Directory.Build.props`, and root `Directory.Packages.props`.
  Per-package CPM drift was Codex's main objection to D19.
- **D21 (new)** — Premise 5 (TS semantic equivalence) is narrowed:
  v0.1.0 mirrors the **Unity public surface** plus the OTP login flow
  semantics shared with `peak-sdk-browser`. Google OAuth, IndexedDB
  Turnkey-key-pair storage, and `getAuthenticatedSdk` are explicitly
  out of v0.1.0 scope (revisited in v0.2+).
- **D22 (new)** — Upstream sources are mirrored into
  `upstream-snapshots/<name>/` at pinned commit SHAs documented in
  `upstream-snapshots/SOURCES.md`. The mirror is read-only; resync is a
  scripted operation (`scripts/sync-upstream.sh`) and produces a single
  reviewable commit per source.
- **D23 (new, amended)** — OpenAPI spec is sourced from
  `KyuzanInc/peak` **`main`** (latest commit, not a release tag), recorded
  as the exact HEAD commit in
  `upstream-snapshots/peak-server-openapi/PIN.md` so the snapshot stays
  reproducible while tracking main. The codegen runs inside this repo;
  drift CI compares the generated artefacts against the committed
  `packages/peak-public-api-client-csharp/src/`.
- **D24 (new)** — C# OpenAPI client generation runs **in this repo**
  (Option B), not as an extra generator inside peak's `openapi:generate`
  (Option A). Same engine (`openapi-generator-cli` core `7.9.0`) and same
  source spec, but the C# client is generated here from the synced
  snapshot and drift-checked in CI. Option A was declined because the C#
  client's output belongs to this repo, peak is a Node/Nest repo (adding a
  C# artifact crosses the repo boundary D23 draws), and the snapshot model
  keeps peak free of C#-build concerns. **Accepted cost:** the trigger is an
  operator-run resync, not automatic on a peak merge; automatic cross-repo
  dispatch is the deferred B+ follow-up.

## What stays as-is from the monorepo plan

- Approach A (two NuGet packages: Turnkey + Peak) plus
  `peak-public-api-client-csharp` (internal-only generated client) and
  `peak-sdk-csharp-unity` (D20 platform adapter) — unchanged.
- D1 (Http surface), D2 (sync + async), D3 (1-tier IStorage), D6
  (System.Text.Json + source generator), D7 (PeakError pattern), D11
  (dual-target `netstandard2.1;net8.0`), D12 (PR 4.5 SecureStorage in
  v0.1.0 timeline), D13 (internal-only generated client), D14 (PR 0
  task H IL2CPP escalation), D15 (Unofficial Turnkey SDK disclaimer),
  D16 (Unity-shaped public surface for turnkey-sdk-csharp), D20 (Unity
  adapter as its own package).
- The Boil-the-Lake test coverage target (RFC 9180 §A.3 HPKE, RFC 5869
  HKDF, NIST CAVP P-256, Turnkey sample bundles, Turnkey-issued JWT
  positive/negative). PR 1 fixture list is **expanded**, not reduced
  (C-A2).
- The MIT license + Unofficial disclaimer wording in LICENSE and
  README.public.md.

## Open questions

| # | Question | Owner | Deadline | Status |
|---|---|---|---|---|
| OQ-N1 | `peak-server` ref to source OpenAPI from. | Komy CTO / TJ | PR 2 start | ✅ Resolved — track `main` HEAD (user directive 2026-05-30), not a release tag; each sync records the exact commit SHA in PIN.md. |
| OQ-N2 | Paid third-party crypto audit vendor + budget for v1.0.0 (was OQ in the monorepo plan but de-emphasised by D17; restored as v1.0.0 blocker by D17-CLAR) | Komy CTO | v1.0.0 cut | ⬜ Open |
| OQ-N3 | Submodule vs scripted-snapshot for `upstream-snapshots/`. Submodule keeps history pointers but adds clone friction. Snapshot keeps reviewability but loses upstream history. **Recommend snapshot + scripted resync.** | TJ | W0b start | ⬜ Open, default = snapshot |
| OQ-N4 | Is a NuGet `local file feed` enough for PR 5 (Unity thin-adapter) consumption, or does PR 5 need GitHub Packages? | Komy CTO | PR 5 start | ⬜ Open, default = local file feed |

---

## PR 0 — Bootstrap + feasibility (W0a + W0b + W0c)

### Goal

Single-PR repository bootstrap: enough scaffolding that PR 1 can land
crypto code into a buildable repo. Plus the upstream snapshots + OpenAPI
sync infrastructure so PR 1 / PR 2 have stable inputs.

### Deliverables

Repo layout:

```
peak-sdk-csharp.sln                       Root solution
Directory.Build.props                     Shared props (LangVersion 10, Nullable enable, TreatWarningsAsErrors)
Directory.Packages.props                  Central package management; pin BouncyCastle.Cryptography [2.5.0], System.Text.Json 8.0.5, xunit 2.9.2, etc.
nuget.config                              Local file feed for PR 5 consumption

upstream-snapshots/
  SOURCES.md                              Pin map: source repo, commit SHA, mtime, scripted resync command
  peak-sdk-unity/                         Snapshot of KyuzanInc/peak-sdk-unity @ fc560e8 (per peak repo as of 2026-05-27)
  turnkey-sdk-unity/                      Snapshot of KyuzanInc/turnkey-sdk-unity @ 039d8e4
  turnkey-official-src/                   @turnkey/crypto@2.8.9, @turnkey/api-key-stamper@0.6.0, @turnkey/http@3.16.0 tgz-extracted source
  peak-server-openapi/
    PIN.md                                Pin tag + retrieved-at + checksum
    public-api.yaml                       Spec snapshot

scripts/
  sync-upstream.sh                        Resync workflow (operator-driven; emits one commit per source)
  check-cpm-drift.sh                      Stub for now (CPM is centralised so drift is trivial; placeholder kept)

codex-crypto-reviews/
  README.md                               Review process + prompt template + pass criteria
  codex-crypto-review.sh                  Codex multi-round invocation helper
  turnkey-source-pins.md                  Pins @turnkey/* commit SHAs (links to upstream-snapshots/turnkey-official-src/)
  unity-source-pins.md                    Pins peak-sdk-unity / turnkey-sdk-unity port-base commits (links to upstream-snapshots/)

docs/
  architecture.md                         Package overview, layering, Unity-vs-generic boundary
  development.md                          Local build/test, dotnet-version, mac/win/linux notes
  security/
    storage-threat-model.md               Threat model (storage attack surface, plaintext storage caveat, opt-in mechanism)
    crypto-port-policy.md                 D17-CLAR: Codex review is evidence not audit; audit blocks v1.0.0
  sync-rules.md                           Resync procedures for upstream-snapshots and OpenAPI

.github/workflows/
  csharp-ci.yml                           Build + test on ubuntu / windows / macos; framework matrix per TFM consumer test

plans/
  plans-peak-sdk-csharp.md                This file
```

### Verification

```
dotnet --version                                       # >= 8.0
dotnet build peak-sdk-csharp.sln -c Release            # clean (no packages yet, just solution)
dotnet format --verify-no-changes                      # editorconfig compliance
```

### Estimate

CC ~3-5 hours of generation; human verification ~1 day.

---

## PR 1 — turnkey-sdk-csharp vertical slice (W1)

> **HISTORICAL — superseded by `plans/plans-turnkey-import.md`.**
> The Turnkey port now lives in
> [`KyuzanInc/turnkey-sdk-csharp`](https://github.com/KyuzanInc/turnkey-sdk-csharp)
> and this repo consumes the `KyuzanInc.Turnkey.Sdk` NuGet package.
> The paths referenced in this section (`packages/turnkey-sdk-csharp/`,
> `codex-crypto-reviews/`, `upstream-snapshots/turnkey-sdk-unity/`,
> `upstream-snapshots/turnkey-official-src/`,
> `docs/security/crypto-port-policy.md`) have been deleted from this
> repo. Crypto changes go to the external repo; the consumer-side bump
> procedure is in [`docs/sync-rules.md`](../docs/sync-rules.md).
> Kept here for historical context only — do not follow these steps
> for new work.

### Goal (rewritten per C-A1, replaces D10 references)

Port `Crypto.cs`, `ApiKeyStamper.cs`, `Encoding.cs`, `Http.cs`,
`UnityConstants.cs` from `upstream-snapshots/turnkey-sdk-unity/Runtime/`
to `packages/turnkey-sdk-csharp/src/` such that the result is logically
1:1 with `upstream-snapshots/turnkey-official-src/` (TypeScript) and
preserves the Unity-shaped public surface (D16). Replace all
`UnityEngine.JsonUtility.ToJson` with
`System.Text.Json.JsonSerializer.Serialize(value, TurnkeyJsonContext.Default.<T>)`
via a source-generated context (D6). Replace `Newtonsoft.Json.Linq` in
`Crypto.cs` (only used by `EncryptPrivateKeyToBundle` /
`DecryptExportBundle`) with `System.Text.Json.JsonDocument`.

BouncyCastle 2.5.0 stays as the primitive backend
(ECDSA / ECDH / AES-GCM / SHA-256). HPKE / HKDF / Tonelli-Shanks /
bundle parse / JWT verify are ported source-for-source from the
TypeScript upstream.

### Expanded test fixture matrix (per C-A2)

For each algorithm path, **multiple** vectors and a non-positive case:

| Path | Fixture sources |
|---|---|
| Hex encode/decode | RFC 4648 §8 + invalid char + odd length + 0-byte + 256-byte |
| Base58 / Base58Check | Bitcoin BIP-13 vectors + bad checksum + 1-byte + leading-zero |
| Tonelli-Shanks `ModSqrt` | NIST P-256 prime + non-residue throw + p=3 mod 4 fast path |
| HKDF Extract + Expand | RFC 5869 A.1, A.2, A.3 (SHA-256 only — Turnkey uses SHA-256) |
| ECDSA sign | NIST CAVP P-256 + RFC 6979 deterministic vectors |
| ECDSA verify | Valid + tampered signature + wrong key + low-S enforcement |
| Compress/UncompressRawPublicKey | NIST P-256 sample points (even Y / odd Y) + invalid prefix + out-of-range |
| HPKE Decrypt | RFC 9180 §A.3 base mode known-vector |
| HPKE Encrypt | Round-trip vs HpkeDecrypt **plus** a fixed-ephemeral hook fixture that produces a known ciphertext byte-for-byte; without this, ephemeral drift goes undetected |
| Credential bundle | Turnkey sandbox sample (1 fixture committed) + bad checksum + bundle shorter than 33 bytes |
| Export bundle (current envelope) | Turnkey sandbox sample + tampered signature + org-id mismatch |
| Export bundle (legacy envelope) | Turnkey sandbox sample with `signedData`/`signature` fields + tampered |
| Encrypt private key to bundle | Turnkey sandbox sample import-bundle + mismatched org-id throw + mismatched user-id throw |
| JWT verify | Turnkey-issued sample (positive) + bad signature + wrong length signature + bad header structure |

Fixtures live in `packages/turnkey-sdk-csharp/tests/Fixtures/` with a
`README.md` citing each fixture's origin. Fixtures are committed text
files; no network access during test runs.

### File layout (single-repo, root sln per D19-CLAR)

```
peak-sdk-csharp.sln                            (root)
Directory.Build.props                          (root, shared)
Directory.Packages.props                       (root, shared, CPM)

packages/turnkey-sdk-csharp/
├── README.md                                  internal
├── README.public.md                           NuGet-facing, Unofficial disclaimer + 1:1 port disclosure (D15 + D17-CLAR)
├── src/
│   ├── turnkey-sdk-csharp.csproj              <PackageId>KyuzanInc.Turnkey.Sdk</PackageId>; TargetFrameworks netstandard2.1;net8.0
│   ├── Encoding.cs                            Port — direct (Unity-independent already)
│   ├── Crypto.cs                              Port + System.Text.Json migration of EncryptPrivateKeyToBundle / DecryptExportBundle
│   ├── CryptoConstants.cs                     Renamed from UnityConstants.cs; Unity-independent
│   ├── ApiKeyStamper.cs                       Port; replace JsonUtility.ToJson with TurnkeyJsonContext
│   ├── Http.cs                                Port; same JSON replacement; keep nested DTOs (D16)
│   └── TurnkeyJsonContext.cs                  System.Text.Json source-generated context (D6, IL2CPP safety)
└── tests/
    ├── turnkey-sdk-csharp.Tests.csproj        TargetFrameworks net8.0
    ├── EncodingTests.cs
    ├── CryptoTests.cs
    ├── ApiKeyStamperTests.cs
    ├── HttpTests.cs
    ├── Fixtures/                              See matrix above
    └── E2E/
        └── TurnkeyWhoamiTests.cs              env-gated; CI runs only when TURNKEY_TEST_ORG_API_KEY is set

codex-crypto-reviews/
├── Crypto.cs-r1-<date>.md
├── Crypto.cs-r2-<date>.md
├── Crypto.cs-r3-<date>.md
├── ApiKeyStamper.cs-r1..r3.md
└── Http.cs-r1..r3.md

docs/security/storage-threat-model.md          first draft committed in PR 1
docs/security/crypto-port-policy.md            first draft committed in PR 1
```

### Success criteria

- [ ] `dotnet build peak-sdk-csharp.sln -c Release` PASS on both target
      frameworks (netstandard2.1 + net8.0)
- [ ] `dotnet test peak-sdk-csharp.sln -c Release` PASS with line
      coverage ≥ 80 % (Crypto / ApiKeyStamper / Http / Encoding)
- [ ] CI green on `ubuntu-latest` + `windows-latest` + `macos-latest`
- [ ] Codex multi-round review evidence: ≥ 3 rounds × 3 files = 9
      evidence files, all reporting "no logic divergence" in the final
      round
- [ ] `upstream-snapshots/turnkey-official-src/` is present and pinned
      with SHAs in `codex-crypto-reviews/turnkey-source-pins.md`
- [ ] `docs/security/storage-threat-model.md` + `crypto-port-policy.md`
      first drafts committed
- [ ] `README.public.md` + `<Description>` in `.csproj` contain the
      Unofficial Turnkey SDK disclaimer (D15) **and** the 1:1 port +
      Codex-reviewed-not-audited disclosure (D17-CLAR)
- [ ] PublicApiGenerator baseline matches the Unity port surface (no
      extra interfaces like `IApiKeyStamper`; nested DTOs preserved) —
      D16 enforced

### Estimate (revised)

CC pure implementation ~6-10 hours per file × 3 crypto files +
fixtures + tests + Codex multi-round + docs = **~3-5 days CC** in this
session if no human review iteration; calendar with human review
**~1.5-2 weeks**.

---

## PR 2 — peak-sdk-csharp + OpenAPI codegen + OTP login + IStorage (W2)

### Goal

Add:

- `packages/peak-public-api-client-csharp/` — internal-only OpenAPI
  generated client. Generation runs against
  `upstream-snapshots/peak-server-openapi/public-api.yaml` (D23). The
  package is referenced with `IsPrivateAssets="true"`-equivalent
  hardening so consumers cannot accidentally take a dependency on
  generated types.
- `packages/peak-sdk-csharp/` with:
  - `PeakClient` + `PeakClient.Initialize(opts)` + `PeakClient.Initialize(apiKey, apiUrl)` static factories (D2)
  - `PeakError` + `PeakErrorCode` (string enum) + `ApiResponseContext` + `ToPeakError` helper (D7)
  - `IStorage` + `InMemoryStorage` (D3); `Get` + `GetAsync` both available (D2)
  - `ILogger<T>` injection with `NullLogger<T>.Instance` default
  - `IPeakHttpClient` abstraction + default `HttpClient`-backed
    implementation; `IHttpClientFactory`-recommended pattern documented
  - `AuthService` — `InitOtpLogin` + `CompleteOtpLogin`; the latter
    auto-saves into the configured `IStorage`
  - `Authenticate()` + `AuthenticateAsync(CancellationToken)` (D2)
  - `AuthenticatedPeakClient` skeleton (services hung off it land in PR 4)

### Notable wiring

- `PeakClient` ensures `EnsureAuthenticated()` runs at the top of every
  authenticated method (semantic equivalent of TS `ensureAuthenticated`,
  D21 narrowing accepted).
- Session JWT expiry surfaces as `PeakError(Code = "SDK_SESSION_EXPIRED")`
  — not a separate exception type (TS parity).
- Logger never logs raw key material; `PeakError.LogContext` is the
  redaction-aware payload.

### Estimate

CC ~5-7 days in calendar terms once codegen + DTO wrapper is wired.

---

## PR 3 — Godot + console smoke + Unity reference example (W3)

### Goal

Show the SDK working under three hosts:

1. `examples/peak-sdk-console/` — net8.0 console app, executes the OTP
   login flow against a Peak test environment using env vars.
2. `examples/peak-sdk-godot/` — Godot 4.x project that uses
   `KyuzanInc.Peak.Sdk` (netstandard2.1 path) via a NuGet reference.
3. `examples/peak-sdk-unity-reference/` — a Unity sample project (added
   as a sibling, not a submodule) consuming the packages via local
   `.nupkg` from `dotnet pack`. Used to exercise the IL2CPP smoke path
   that the original plan delegated to `peak-sdk-unity-example`.

The Unity sample is intentionally **minimal**: just the asmdef + a
single MonoBehaviour that calls `PeakClient.Initialize`. iOS / Android
Player build smoke is human-driven (Komy + TJ) using XCode + Android
Studio, with results captured in
`docs/operations/il2cpp-smoke-<date>.md`.

### Estimate

CC ~1-2 days. Manual IL2CPP smoke is a separate human checklist.

---

## PR 4 — remaining v0.1.0 API surface (W4)

`AccountService` and `PrivateKeyService` are ported under
`packages/peak-sdk-csharp/src/Services/Authenticated/` with their
v0.1.0 method set (3 + 3 + 1 = 7 methods, see status table). Test
coverage ≥ 80 %, the existing E2E flag covers happy-path round-trips.

---

## PR 4.5 — SecureStorage (W4.5, per C-B1 + C-B2 hardening)

Beyond the original D20 package split:

- `UnsafePlaintextPlayerPrefsStorage` requires **both**:
  - A compile-time symbol `PEAK_UNSAFE_STORAGE_OPT_IN` set on the
    consumer's csproj, **and**
  - A constructor argument `bool acknowledgePlaintext = true`. The
    parameter has no default and the constructor throws if `false`.
- `DpapiSecureStorage` is the only built-in `ISecureStorage` in the
  core package. `ISecureStorage.IsAvailable` returns `false` on Linux,
  macOS, and non-Windows .NET hosts; consumer code MUST handle the
  false branch. The threat model lists each platform.
- `peak-sdk-csharp-unity` adds `KeychainSecureStorage` and
  `KeyStoreSecureStorage` (iOS / Android via Unity APIs). Both have a
  documented Unity smoke checklist; CI builds only the netstandard2.1
  surface against Unity reference assemblies.

`docs/security/storage-threat-model.md` is **normative** as of PR 4.5;
it lists the release-blocker checks for v0.1.0.

---

## PR 7 — NuGet publish workflow + public docs (W7)

Adds:

- `.github/workflows/csharp-publish.yml` — `dotnet pack` + `nuget push`
  on tag.
- `docs/sdks-and-tools/peak-sdk-csharp/` — internal mirror of the
  user-facing reference; `apps/peak-public-docs/...` mirroring is
  deferred to whenever the peak monorepo wants to vendor a snapshot.

PR 5 (turn `peak-sdk-unity` into a thin adapter) is left as a separate
PR in `KyuzanInc/peak-sdk-unity` — outside this plan, this repo only
provides the packages it consumes.

PR 6 (peak monorepo submodule bump) is **out of scope** per the
user directive ("do not update peak monorepo").

---

## Wc1 — Crypto fuzz + property tests (front-loaded from Phase 2)

Per C-C1: a paid audit is not in v0.1.0, so we front-load every cheap
defence-in-depth we can:

- Property tests with FsCheck for HPKE round-trip on random inputs
- Bit-flip fuzz on credential / export bundles → expect throw
- Invalid-curve points fed into `UncompressRawPublicKey` and
  `Crypto.HpkeDecrypt` (must throw before ECDH)
- Low-S validation on signatures (BIP-62)
- DER edge cases for `SignPayload` (high-bit r and s, leading zero, etc.)
- JWT signature with non-64-byte signature → must return false

Coverage estimate: +1 week CC after PR 1.

## Wc2 — Cross-SDK conformance suite (front-loaded)

A small `tests/conformance/` project pulls
`upstream-snapshots/turnkey-official-src/` and runs TypeScript via Node
(`npx tsx`) against the same fixtures the C# port uses, then diffs the
outputs. Lives at the end of W1 / W4 and runs in CI on a schedule.

---

## Cross-cutting policies

- **CODEOWNERS** — Komy CTO + TJ own everything under `packages/turnkey-sdk-csharp/src/Crypto.cs` and `docs/security/`.
- **Dependabot** — enabled for NuGet + GitHub Actions (config in PR 0).
- **SBOM** — generated by the publish workflow (PR 7) with `dotnet sbom-tool`.
- **Branch protection** — `main` requires CI green + 1 approving review + signed commits. Bypass off.
- **Secret scanning** — GitHub native + `gitleaks` pre-commit (hook config in PR 0).

## GSTACK REVIEW REPORT

| Review | Trigger | Why | Runs | Status | Findings |
|--------|---------|-----|------|--------|----------|
| Codex Review | `/codex` consult | Independent 2nd opinion on the upstream plan | 1 | CLEAR (issues found, all P1 accepted) | 9 P1 findings reflected as C-A1, C-A2, C-B1, C-B2, C-C1, C-D1, C-D2, C-E1, C-E2 — see adjudication table |
| Eng Review | `/plan-eng-review` | Architecture & tests | 0 | Inherited from upstream plan eng review (8 issues, 5 user-decided D1-D5, 3 auto-resolved) | — |
| CEO Review | `/plan-ceo-review` | Scope & strategy | 0 | Skipped — scope was locked in upstream plan and remains unchanged by Codex findings | — |
| Design Review | `/plan-design-review` | UI/UX | N/A | library package, no UI | — |
| DX Review | `/plan-devex-review` | Developer experience | 0 | Recommended for v0.1.0 (NuGet adopter onboarding); skipped this round | — |

**CODEX:** 9 P1 findings, all accepted and reflected as
D17-CLAR / D19-CLAR / D21 / D22 / D23 plus the Wc1, Wc2, W0a, W0b, W0c
workstreams.
**UNRESOLVED:** OQ-N1 (peak-server tag pin), OQ-N2 (paid audit vendor),
OQ-N3 (snapshot vs submodule), OQ-N4 (local feed vs GitHub Packages).
None block PR 0 or PR 1 start.
**VERDICT:** CLEARED — Codex review absorbed, plan adapted for
single-repo bootstrap, Codex P1 findings reflected as new decisions.
PR 0 implementation starts immediately.
