# Design — Unity reference example with the full OTP login flow (IL2CPP smoke vehicle)

- **Date:** 2026-06-09
- **Status:** Draft — **r2** (cleared one multi-aspect review round: four parallel
  lens agents — engineering-correctness, Unity/IL2CPP domain, completeness/skeptic,
  security/convention — plus an independent OpenAI Codex review. r1 was bounced
  **NOT-READY** by the domain, completeness, and security lenses and Codex. Four
  distinct BLOCKERs were found across the round — two from the Unity lens (missing
  `packages.config`; insufficient `ProjectSettings`/scene), one from
  completeness + Codex (the `"ETHEREUM"` `chainType`), one from security + Codex
  (the `[SerializeField]` API-key scene-leak) — plus the MAJORs; all are resolved
  below. A round-2 confirmation pass (Codex + the Unity and completeness/security
  lenses) returned READY / READY / READY-WITH-FIXES; its one carried MAJOR (the
  `packages.config` closure must enumerate the *complete* graph, not just the
  first-order six) is folded into D8. See the change log in §14.)
- **Issue:** [KyuzanInc/peak-sdk-csharp#24](https://github.com/KyuzanInc/peak-sdk-csharp/issues/24)
- **Topic:** Add `examples/peak-sdk-unity-reference/` — a minimal Unity project
  that consumes `KyuzanInc.Peak.Sdk` via a **local `.nupkg` file feed** and
  demonstrates the full OTP login flow (`InitOtp → CompleteOtp → Authenticate →
  ListAccounts → Import/Export`) through an IMGUI UI. The **primary purpose is an
  IL2CPP AOT smoke**: prove that `HttpClient` + System.Text.Json source-gen +
  `async/await` state machines + BouncyCastle (via `PeakCrypto`) survive IL2CPP
  AOT stripping in a real Unity Player build.
- **Realizes:** the in-repo Unity smoke vehicle that decision **C-E2**
  (`plans/plans-peak-sdk-csharp.md`, "IL2CPP smoke depends on
  `peak-sdk-unity-example` which is not in this repo … PR 3 hosts a minimal
  Unity-shaped example") promised, and that the 2026-06-03 STJ-only design
  deferred as "OQ3 — full Unity IL2CPP runtime smoke … follow-up in
  `peak-sdk-unity` (W5)"
  ([2026-06-03 design §10/§11](2026-06-03-unity-il2cpp-stj-only-consumer-path-design.md)).
  This issue brings the IL2CPP *runtime* smoke **into this repo** instead of
  waiting on the external W5 adapter.
- **Scope note vs the original W3 plan.** The W3 plan row framed the Unity sample
  as *intentionally minimal* — "just the asmdef + a single MonoBehaviour that
  calls `PeakClient.Initialize`". Issue #24 **deliberately widens** that to the
  full OTP + wallet flow because a bare `Initialize()` call does not exercise the
  things IL2CPP actually breaks (HTTP + STJ source-gen + async state machines +
  BouncyCastle). The widening is the point of the smoke; this design records it
  as an explicit, signed-off scope change (Decision D2), not scope creep. (W3's
  Console + Godot thirds stay deferred — §2 non-goals.)

---

## 1. Context

### 1.1 What exists today

- `KyuzanInc.Peak.Sdk` (`packages/peak-sdk-csharp/src/`) is the published,
  consumer-facing package: `PackageId=KyuzanInc.Peak.Sdk`, `Version=0.1.0-alpha.2`,
  `TargetFrameworks=netstandard2.1;net8.0;net8.0-windows`. Unity restores the
  **`netstandard2.1`** asset.
- There is **no `examples/` directory** in the repo yet. The example referenced
  by the issue for UX parity (`peak-sdk-unity-example/.../PeakSdkDemo.cs`) lives
  in the **peak monorepo**, not here — so "parity with `PeakSdkDemo.cs`" below
  means "modeled on the monorepo demo," not a vendored or pinned source.
- The repo's `nuget.config` declares a `local-feed` source (`./local-feed`) with
  `packageSourceMapping` routing `KyuzanInc.Peak.*` → local feed and
  `KyuzanInc.Turnkey.*` → `github-kyuzan` (GitHub Packages). The example's own
  `NuGet.config` will deliberately diverge from this (D3/§5.1).
- The CI job that packs the SDK locally and restores it from `./local-feed` on
  every PR is **`consumer-restore-check` in `.github/workflows/csharp-ci.yml`**
  (`dotnet pack packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj` → `./local-feed`,
  then a locked restore over the `[netstandard2.1, net8.0]` matrix). It resolves
  the transitive `KyuzanInc.Turnkey.Sdk` from `github-kyuzan` **with
  `GITHUB_TOKEN`**. (The separate `consumer-smoke.yml` consumes the **published**
  GitHub-Packages package *post-publish* via `dotnet add package`; it is **not**
  the local-pack signal — r1 mis-cited it.)
- IL2CPP smoke results are expected at `docs/operations/il2cpp-smoke-<date>.md`
  (per the W3 plan narrative). **`docs/operations/` does not exist yet** — this
  design creates it.

### 1.2 The verified public surface the example must call (normative — D6)

The issue ships an API-mapping table; it is **mostly right but stale in four
places**, and it omits the **valid argument domains** that the example's calls
depend on. The authoritative surface, read from source on 2026-06-09, is:

| Capability | Real signature / valid args (verified) | Issue table said |
| --- | --- | --- |
| Init | `PeakClient.Initialize(string apiUrl, string projectApiKey)` → `PeakClient` (string overload; the options-bag overload needs C# 9+/11+ and is intentionally avoided for the netstandard2.1 consumer) | ✓ correct |
| Send OTP | `InitOtpLoginAsync(string email, CancellationToken)` → `Task<InitOtpLoginResponse?>` (**nullable**; `.OtpId` is `string?`) | ✗ omitted the nullable return |
| Complete OTP | `CompleteOtpLoginAsync(string email, string otpId, string otpCode, bool signup = true, CancellationToken)` → `Task<CompleteOtpLoginResult>` | ✗ omitted the **`signup`** parameter |
| Authenticate | `Authenticate()` → `AuthenticatedPeakClient` (sync); also `AuthenticateAsync()` | ✓ correct |
| Logout | `Logout()` → `void` (deletes the session; a later `Authenticate()` throws `NotAuthenticated` → UI returns to Idle) | — |
| List accounts | `ListAccountsAsync(CancellationToken)` → `Task<AccountResponse[]>` | ✓ correct |
| List addresses | `ListAccountAddressesAsync(string accountId, CancellationToken)` → `Task<AccountAddressResponse[]>` | — |
| Init import | `InitImportPrivateKeyAsync(CancellationToken)` → `Task<InitImportPrivateKeyResult>` (`.ImportBundle/.OrganizationId/.UserId`) | name ✓ |
| Encrypt bundle | `PeakCrypto.EncryptPrivateKeyToBundle(PeakCrypto.EncryptPrivateKeyToBundleParams { PrivateKey, ImportBundle, OrganizationId, UserId, KeyFormat })` → `string` | ✗ takes a **params object** |
| Complete import | `CompleteImportPrivateKeyAsync(string encryptedBundle, string chainType, CancellationToken)` → `Task<CompleteImportPrivateKeyResult>`. **`chainType` valid domain = exactly `{"evm","solana"}`** (case-insensitive; `PrivateKeyService` switches on `chainType.ToLowerInvariant()` and throws `PeakError(InvalidArgument, "Unsupported chain type: …. Supported: evm, solana")` for anything else — `"ETHEREUM"` is **NOT** valid). | name ✓; **value domain undocumented** |
| Generate target key | `PeakCrypto.GenerateP256KeyPair()` → `PeakCrypto.KeyPair` (`.PrivateKey/.PublicKey/.PublicKeyUncompressed`). NB: distinct from `Models.KeyPair` — reference `PeakCrypto.KeyPair` or `var` to avoid the ambiguity if `using …Models;` is present. | — |
| Export | `ExportPrivateKeyAsync(string address, string targetPublicKey, CancellationToken)` → `Task<ExportPrivateKeyResult>` (`.ExportBundle/.OrganizationId`). Throws `InvalidArgument` on an empty `address`. | name ✓ |
| Decrypt bundle | `PeakCrypto.DecryptExportBundle(PeakCrypto.DecryptExportBundleParams { ExportBundle, EmbeddedKey, OrganizationId, KeyFormat, ReturnMnemonic })` → `string` (raw private-key hex when `ReturnMnemonic=false`) | ✗ takes a **params object** |
| Errors | `PeakError : Exception` with `.Code` (string from `PeakErrorCode.*`) | ✓ correct |

The example calls exactly the left column. **D6** makes this table — *including the
`chainType ∈ {evm, solana}` value domain* — normative; any divergence is a bug.
The plan defines a shared `const string ChainTypeEvm = "evm"` so the value cannot
drift (the r1 `"ETHEREUM"` bug is exactly this drift).

### 1.3 The dependency reality the issue under-states (load-bearing)

The issue claims a local `.nupkg` feed keeps the example self-contained **without
GitHub Packages auth**. That is only partly true and is the single most important
constraint here.

`dotnet pack packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj` produces
**`KyuzanInc.Peak.Sdk.nupkg` only** (the generated client and the test project are
`IsPackable=false`). But a Unity consumer of `KyuzanInc.Peak.Sdk` (netstandard2.1)
needs a **transitive closure**:

- `KyuzanInc.Turnkey.Sdk [0.1.0-alpha.0]` — **GitHub Packages only** (not on
  nuget.org). `PeakCrypto` delegates to it, so it is on the runtime closure.
- `System.Text.Json 8.0.5`, `Microsoft.Extensions.Http 8.0.1`,
  `Microsoft.Extensions.Logging.Abstractions 8.0.2` — nuget.org. On netstandard2.1
  these pull the usual second-order transitives (`Microsoft.Extensions.Options`,
  `System.ComponentModel.Annotations`, etc.) — all nuget.org, no auth impact.
- `BouncyCastle.Cryptography` (the architecture doc pins `2.5.0`) — transitive
  **via Turnkey** — nuget.org.

So a Unity project that only adds a local feed for `KyuzanInc.Peak.*` **cannot
restore** — `KyuzanInc.Turnkey.Sdk` is unreachable. "No GitHub auth" is achievable
**only if the example also vendors the Turnkey `.nupkg`** into its local feed
(D3). **Critically, NuGetForUnity (NFU) does not do full transitive resolution the
way `dotnet restore` does** — it restores what `Assets/packages.config` lists.
So the example must **explicitly enumerate the whole closure** in `packages.config`
(D8), not rely on transitive pull-through.

### 1.4 What can and cannot be verified in *this* repo's CI

This is a Unity project; there is **no licensed Unity runner in CI**:

- **Cannot** in-repo CI: open the project in Unity, compile the MonoBehaviour (it
  references `UnityEngine`), run Play mode, or do the IL2CPP Player build.
- **Can** in-repo CI / locally: (a) the solution build/test/pack stays green
  (the example is not in the `.sln`); (b) `consumer-restore-check` already proves
  `dotnet pack` yields a consumable nupkg; (c) **a new net8.0 API-conformance
  shim** (D13) mechanically compiles the example's SDK-call expressions (minus
  `UnityEngine`) against the packed assembly — a real automated floor that would
  have caught the r1 signature/`chainType` drift class; (d) markdown link-lint.

Honest consequence: **zero of issue #24's five acceptance criteria are
CI-enforceable** (all five need Unity). The merge gate is the D13 conformance
compile + a human static review + the human IL2CPP smoke log — **not** CI green.
The design states this bluntly rather than implying CI proves the smoke (R1).

---

## 2. Goals and non-goals

### Goals

1. From a clean checkout, a documented setup (`prepare-feed.sh`: pack the SDK +
   vendor the Turnkey nupkg) lets a developer **open
   `examples/peak-sdk-unity-reference/` in Unity 6000.0.x with zero errors** and
   `KyuzanInc.Peak.Sdk` resolved via the local feed.
2. The example **runs the full OTP login flow end-to-end in Editor Play mode**:
   `InitOtpLoginAsync → CompleteOtpLoginAsync → Authenticate → ListAccountsAsync`,
   driven by an IMGUI state machine (`Uninitialized → Idle → AwaitingOtp →
   Authenticated`).
3. The import/export wallet calls and the `PeakCrypto` round-trip are **callable
   from the UI**, exercising BouncyCastle + STJ under IL2CPP, not just HTTP.
4. **An IL2CPP Standalone Player build succeeds and the flow runs on a Player**
   (human-verified), results logged in `docs/operations/il2cpp-smoke-<date>.md`.
5. **No UniTask**; `async void` MonoBehaviour callbacks with explicit `try/catch`
   and no `ConfigureAwait(false)` in the handlers (D7), consistent with the SDK's
   standard-`Task` posture.
6. Adding the example **does not break the existing solution build/test/pack** and
   does not pull Unity into `peak-sdk-csharp.sln`.

### Non-goals

- **Console and Godot examples** (the other two thirds of W3). Deferred.
- **A licensed Unity build in CI.** No Unity runner; the IL2CPP smoke is a human
  checklist (Goal 4), not a CI gate.
- **A production-grade UI.** IMGUI (`OnGUI`) only; one scene; no uGUI/UI-Toolkit,
  no art.
- **Shipping the example as a NuGet package** or adding it to the `.sln`.
- **Turning `peak-sdk-unity` into a thin adapter (W5).** Separate repo; this
  example is a *reference consumer*, not the adapter.
- **Secure-storage demonstration.** Default `InMemoryStorage` (session does not
  persist across runs). `ISecureStorage` is out of scope.
- **Real fund movement / production keys.** Import/export operate on **test** key
  material against a Peak environment the smoke-runner controls (§9 OQ6); the
  example warns test-keys-only (D9, R5).

---

## 3. Decisions

### D1 — Consume via a local `.nupkg` file feed (as the issue specifies)
The Unity project gets its own `NuGet.config` pointing at a project-local
`LocalFeed/`, populated by `dotnet pack`; NFU restores from it. Matches the issue
and the W3 plan; keeps the example tracking the working-tree SDK rather than a
published version. *Rejected:* consume the published GitHub-Packages SDK — would
not exercise un-released changes and still needs auth.

### D2 — Full OTP + wallet flow, not a bare `Initialize()` (scope widening)
Implement the complete state machine and all §1.2 calls. The IL2CPP failure modes
this smoke exists to catch (HTTP, STJ source-gen, async state machines,
BouncyCastle) are not reached by `Initialize()` alone. Recorded as an explicit
scope change.

### D3 — Vendor the transitive `KyuzanInc.Turnkey.Sdk` nupkg into the local feed (no-auth default), pinned exact
To honor the issue's "no GitHub Packages auth" goal, `prepare-feed.sh` copies the
**exact pinned** `KyuzanInc.Turnkey.Sdk` `.nupkg`
(`~/.nuget/packages/kyuzaninc.turnkey.sdk/0.1.0-alpha.0/…nupkg`, version read from
`Directory.Packages.props` — **not** `sort | tail -1`, which mis-orders
prerelease semver) into `LocalFeed/`, beside the packed `KyuzanInc.Peak.Sdk`.
`System.Text.Json`/`Microsoft.Extensions.*`/`BouncyCastle.Cryptography` come from
nuget.org via NFU. This is **binary package consumption, not source vendoring** —
it copies an already-published, version-stamped `.nupkg`, so it does **not**
violate `CLAUDE.md`'s "do not vendor a copy of any *file* from the Turnkey
package" rule (that rule targets source `.cs` forks that bypass the crypto review
gate; D10 confirms).

**This default knowingly diverges from the repo's documented Unity-consumer
guidance** (`docs/development.md`: "Unity consumers via NuGetForUnity … still need
the GitHub Packages source configured, because `KyuzanInc.Peak.Sdk` pulls
`KyuzanInc.Turnkey.Sdk` transitively"). The issue explicitly chose the no-auth
path, so this design follows it **and updates `docs/development.md`** (§5.7) to
document the vendored-Turnkey local-feed path as the example's approach. The
GitHub-Packages-auth path (mirroring `consumer-restore-check`) is the documented
**alternative** in the example README for developers who already have auth.
*Why default no-auth despite the repo precedent:* the issue's headline AC is
"self-contained without GitHub Packages auth"; user intent wins over the existing
convention, with the divergence made explicit rather than silent.

### D4 — Do not commit `.nupkg`; generate the feed at setup time
Repo-wide `.gitignore` already excludes `*.nupkg`/`*.snupkg`. `LocalFeed/`
contents are generated, not committed. The committed artifact is the Unity project
sources + `NuGet.config` + `prepare-feed.sh` + README. *Rejected:* a `.gitignore`
exception to commit nupkgs (binary blobs + a stale SDK pin).

### D5 — Commit a deterministic Unity project skeleton; gitignore the generated parts
**Commit** (so it opens and builds deterministically, fixing r1 BLOCKER B2):
`Assets/` (scripts, the scene, `.asmdef`, `packages.config`, `link.xml`, and a
`.meta` for **every** file and folder), the **full standard `ProjectSettings/`
set** (not a hand-written "minimal" one), `Packages/manifest.json`, `NuGet.config`,
`prepare-feed.sh`, `README.md`. **Gitignore** Unity's generated churn by basing
the example's `.gitignore` on **GitHub's official `Unity.gitignore`** (not a
hand-rolled list) plus appended lines for `Assets/Packages/` (+ `Assets/Packages.meta`,
the NFU restore output) and `LocalFeed/`. See §5.5 for the exact committed
`ProjectSettings/` files and the IL2CPP-relevant values.

### D6 — The §1.2 surface, including valid argument domains, is normative
The example uses exactly the verified signatures **and** the verified value
domains (`chainType ∈ {evm, solana}`), via shared named constants. (Fixes r1
BLOCKER: `"ETHEREUM"` would throw before any crypto runs.)

### D7 — `async void` handlers: mandatory `try/catch`, no `ConfigureAwait(false)`, single-flight guard
Unity event callbacks are `async void` (the one acceptable place — an entry point
that cannot be awaited). Every handler:
- wraps its body in `try { … } catch (PeakError ex) { … } catch (Exception ex) { … }`
  (an unobserved `async void` exception crashes the Unity log);
- **does not** use `ConfigureAwait(false)`. The SDK uses `ConfigureAwait(false)`
  *internally*, so the continuation that re-enters the example handler must rely on
  the captured `UnitySynchronizationContext` to resume on the **Unity main
  thread** before touching `UnityEngine` state (`_state`, the `_log` buffer
  `OnGUI` reads). Using `ConfigureAwait(false)` in the handler would risk resuming
  post-`await` UI mutation on a thread-pool thread. The smoke asserts the
  post-`await` `Thread.CurrentThread.ManagedThreadId` equals the captured
  main-thread id (exactly the IL2CPP/SynchronizationContext class of bug worth
  catching);
- guards against double-submit with a `_busy` flag (IMGUI re-renders every frame
  and `async void` is not awaited, so a user can fire overlapping requests).

### D8 — `Assets/packages.config` enumerates the **complete** dependency closure (NFU is not transitive)
Commit `Assets/packages.config` (+ `.meta`) listing **every** package NFU must
restore, pinned exact. **NFU does not resolve transitives**, so the file must list
the *whole* runtime graph, not just the first-order set. The **first-order** six
are `KyuzanInc.Peak.Sdk` 0.1.0-alpha.2, `KyuzanInc.Turnkey.Sdk` 0.1.0-alpha.0,
`System.Text.Json` 8.0.5, `Microsoft.Extensions.Http` 8.0.1,
`Microsoft.Extensions.Logging.Abstractions` 8.0.2, `BouncyCastle.Cryptography`
2.5.0 — **but the committed `packages.lock.json` pulls a dozen-plus more
second-order runtime packages** (e.g. `Microsoft.Extensions.Options`,
`Microsoft.Extensions.Primitives`, `Microsoft.Extensions.Logging`,
`Microsoft.Extensions.DependencyInjection(.Abstractions)`,
`Microsoft.Extensions.Configuration.Abstractions`, `System.Buffers`,
`System.Memory`, `System.Numerics.Vectors`, `System.Text.Encodings.Web`,
`System.Threading.Tasks.Extensions`, `Microsoft.Bcl.AsyncInterfaces`,
`System.Diagnostics.DiagnosticSource`, `System.Runtime.CompilerServices.Unsafe`,
`System.ComponentModel.Annotations`) that NFU must **also** restore from nuget.org.
**`packages.config` lists all ~20** (the exact set comes from the lock file), with
versions read from `packages/peak-sdk-csharp/src/packages.lock.json` so they match
what the SDK actually resolves. (Fixes r1 BLOCKER B1: without `packages.config`,
NFU restores nothing and the asmdef fails to compile; an *incomplete*
`packages.config` is the same failure mode one layer in.) **The plan generates the
exact list mechanically from the netstandard2.1 node of `packages.lock.json`** (not
by hand) and verifies it against a real NFU restore — the six-item first-order set
above is the seed, not the finished file.

### D9 — Never serialize the API key; never echo key material; default logger is `NullLogger`
The `projectApiKey` is a Medium-sensitivity secret. It is **not** a
`[SerializeField]` (a serialized field is written into the committed scene/prefab
YAML in plaintext — r1 security BLOCKER). Instead the key is entered at runtime via
an IMGUI field on the Idle screen, with an optional `PEAK_PROJECT_API_KEY`
environment-variable fallback for Standalone convenience; the committed scene
therefore contains **no key**. `apiUrl` stays a non-secret `[SerializeField]`
(default `https://api.peak.xyz` — the real API; the README warns to use test
credentials, §9 OQ6). The example:
- leaves `PeakClientOptions.LoggerFactory` **null → `NullLogger`**, which makes the
  SDK's `Debug`-level request-body logging (OTP code, **import bundle = wrapped
  private key**, signed envelopes) genuinely inert (R5 — stronger than r1's "don't
  raise the level");
- **never renders raw key material or request/response bodies** to the on-screen
  `_log` buffer or `Debug.Log*` — only redacted markers (e.g. `"[export ok:
  0x…(redacted)]"`); the D7 `catch` blocks log `ex.Code`/`ex.Message` only, never
  `ApiResponse.RawResponseBody`;
- a pre-merge `git diff` check confirms the committed scene's serialized fields
  contain no key.

### D10 — Add a narrowly-scoped `CLAUDE.md` carve-out for `examples/**`
`CLAUDE.md` says "Do not introduce dependencies on `UnityEngine` outside
`packages/peak-sdk-csharp-unity/`." That package does **not exist in this repo**
(the Unity adapter is W5, external), so "move the example under it" is not viable.
The rule's intent is to keep `UnityEngine` out of the **shipped SDK assemblies**;
a consumer sample under `examples/` does not modify any shipped package and is not
in the `.sln`. Rather than self-certify an unwritten exception (r1/Codex MAJOR),
this design **edits `CLAUDE.md`** to scope the rule to shipped packages and
explicitly permit `examples/**` consumer samples to reference `UnityEngine`
(§5.7). The dependency arrow stays example→SDK, never SDK→example (no SDK-package
asmdef references the example).

### D11 — Ship a conservative `Assets/link.xml`; set managed stripping explicitly
The smoke's whole reason to exist is IL2CPP stripping. **The STJ leg is
AOT-safe** (the SDK resolves `JsonTypeInfo<T>` strictly via
`PeakJsonContext.Default.GetTypeInfo(...)` with a throw-on-missing guard and **no
reflection/`DefaultJsonTypeInfoResolver` fallback** — verified in
`PeakResponseJson`/`DefaultPeakHttpClient`/`PeakJsonContext`), so "no link.xml for
STJ" is well-founded. **The BouncyCastle leg is the real risk**: `BouncyCastle.Cryptography`
has a documented history of IL2CPP UnityLinker failures under managed stripping,
especially on Android/iOS. So the example **ships a conservative `Assets/link.xml`**
preserving `BouncyCastle.Cryptography` and `KyuzanInc.Turnkey.Sdk` (and, defensively,
`KyuzanInc.Peak.Sdk` + `System.Text.Json`), documented as "start here; remove an
entry only if a build proves it unnecessary." The committed `ProjectSettings.asset`
sets an explicit `managedStrippingLevel` (start **Minimal/Low**, recorded in the
smoke log), because a "no-stripping-issue" conclusion is only valid for the level
actually tested. R7 reframes BouncyCastle stripping as the *expected* mobile
failure mode, not an edge case.

### D12 — Pin NuGetForUnity via a git-URL UPM dependency (deterministic, no registry)
`Packages/manifest.json` pins NFU (the repo's documented "NuGetForUnity 4.x"
baseline) via its **git-URL UPM form**
(`com.github-glitchenzo.nugetforunity` from the GitHub URL at a tagged version) so
the project has no scoped-registry dependency and opens deterministically (a
committed file with deterministic contents — fixes the r1 "deterministic file,
open contents" tension). NFU restores the `packages.config` packages directly from
the `NuGet.config` sources (local feed + nuget.org) into `Assets/Packages/`. The
plan confirms the exact NFU tag and that this NFU version honors a local-path
source (and `packageSourceMapping`; §5.1 has the fallback).

### D13 — Add a net8.0 API-conformance compile shim in CI (the one automated floor)
Because the MonoBehaviour can't compile in CI (it needs `UnityEngine`), add a tiny
**net8.0 console/test project** (not in the shipped `.sln`'s default build, or
gated) that references the **packed `KyuzanInc.Peak.Sdk`** and contains the exact
SDK-call expressions the example uses — same method calls, params-object
shapes, nullability, and the shared `chainType`/`KeyFormat` constants — with the
`UnityEngine` surface stubbed out. Compiling it in CI catches SDK **signature
drift** mechanically (the class of bug r1's human-only review missed). It does
**not** catch semantic value bugs by itself, so the shared `const string
ChainTypeEvm = "evm"` (D6) plus a one-line assertion that the constant is in the
SDK's accepted set is what guards the `chainType` value. The plan decides exact
placement (a `tests/` conformance project vs an `examples/.../conformance/` one)
and CI wiring.

---

## 4. Architecture

### 4.1 File tree (committed)

```
examples/peak-sdk-unity-reference/
├── .gitignore                          # based on GitHub Unity.gitignore + Assets/Packages/ + LocalFeed/ (D5)
├── README.md                           # setup (no-auth default + GitHub-auth alt), test-creds warning, run steps
├── prepare-feed.sh                     # pack the csproj + vendor exact-pinned Turnkey nupkg → LocalFeed/ (D3)
├── NuGet.config                        # LocalFeed (KyuzanInc.*) + nuget.org (rest), example-specific mapping (D3/§5.1)
├── Assets/
│   ├── Scripts/
│   │   ├── PeakExampleDemo.cs (+ .meta) # MonoBehaviour — IMGUI UI + async void (D2/D6/D7/D9)
│   │   └── Peak.Example.asmdef (+ .meta) # asmdef; NFU-DLL refs via .dll.meta Auto-Reference (§5.4)
│   ├── Scripts.meta
│   ├── Scenes/
│   │   ├── SampleScene.unity (+ .meta)  # ONE scene, a GameObject carrying PeakExampleDemo, NO api key (D9)
│   │   └── Scenes.meta  (Assets/Scenes folder meta)
│   ├── link.xml (+ .meta)               # preserve BouncyCastle + Turnkey (+ SDK/STJ) under IL2CPP (D11)
│   ├── packages.config (+ .meta)        # FULL dependency closure, pinned exact (D8)
├── Packages/
│   └── manifest.json                    # NFU pinned via git-URL UPM (D12)
└── ProjectSettings/
    ├── ProjectVersion.txt               # Unity 6000.0.x pin (§9 OQ1)
    ├── ProjectSettings.asset            # scriptingBackend=IL2CPP, apiCompatibilityLevel=NET Standard 2.1,
    │                                     #   managedStrippingLevel=Minimal, allowUnsafeCode as needed (D11/§5.5)
    ├── EditorBuildSettings.asset        # SampleScene in the build list (else IL2CPP build has no scene)
    ├── EditorSettings.asset             # serialization mode / line endings (determinism)
    └── …the rest of the standard committed set (TagManager, DynamicsManager, Physics2DSettings,
        GraphicsSettings, QualitySettings, InputManager, TimeManager, PresetManager, …)
```

Generated (gitignored): `Library/`, `Temp/`, `Logs/`, `obj/`, `Build/`,
`[Mm]emoryCaptures/`, `UserSettings/`, Unity-generated `*.csproj`/`*.sln`,
`Assets/Packages/` (+ `Assets/Packages.meta`, NFU restore output), `LocalFeed/`.

### 4.2 Consumption / dependency flow

```
working tree SDK ──dotnet pack (csproj)──► examples/.../LocalFeed/KyuzanInc.Peak.Sdk.0.1.0-alpha.2.nupkg
~/.nuget cache  ──prepare-feed (exact pin)─► examples/.../LocalFeed/KyuzanInc.Turnkey.Sdk.0.1.0-alpha.0.nupkg
                                            │
          NFU reads Assets/packages.config + NuGet.config, restores into Assets/Packages/:
              KyuzanInc.Peak.Sdk (netstandard2.1)        ◄─ LocalFeed
              KyuzanInc.Turnkey.Sdk                       ◄─ LocalFeed
              System.Text.Json / M.E.Http / M.E.Logging.Abstractions / BouncyCastle.Cryptography ◄─ nuget.org
              (+ second-order: M.E.Options, System.ComponentModel.Annotations — nuget.org)
                                            │
          Peak.Example.asmdef ── compiles against the restored plugin DLLs (Auto-Reference)
                                            │
          PeakExampleDemo (MonoBehaviour) ── PeakClient / AuthenticatedPeakClient / PeakCrypto / PeakError
                                            │
          IL2CPP build (Assets/link.xml preserves BouncyCastle + Turnkey) → Player smoke
```

The example is **never** part of `peak-sdk-csharp.sln`; it consumes the SDK as an
external NuGet consumer would.

### 4.3 UI state machine (IMGUI, parity with the monorepo demo)

```
Uninitialized ──Initialize()──► Idle ──Send OTP──► AwaitingOtp ──Complete Login──► Authenticated
     ▲                                                                                   │
     └──────────────────────────── Logout ──────────────────────────────────────────────┘
```

- **Config**: `[SerializeField] apiUrl` (default `https://api.peak.xyz`);
  `projectApiKey` entered at runtime (IMGUI field, never serialized — D9).
- **Idle**: API-key field + email field + "Send OTP" → `InitOtpLoginAsync`; stash
  `result?.OtpId` (guard the nullable).
- **AwaitingOtp**: OTP-code field + "Complete Login" → `CompleteOtpLoginAsync(email,
  otpId, code)` then `Authenticate()`.
- **Authenticated**: "List Accounts" (`ListAccountsAsync`), "List Addresses"
  (`ListAccountAddressesAsync`), "Import Private Key", "Export Private Key",
  "Logout". A scrollable text area echoes **redacted** results/errors.
- **Import/Export ordering precondition (stated):** Export needs an `address`,
  which a freshly-OTP'd account may not have until an account/address exists.
  The happy path is **Import (creates an address) → List Addresses → Export**; the
  UI **disables Export until an address is known** (else `ExportPrivateKeyAsync`
  throws `InvalidArgument` on an empty address). `_busy` disables all buttons
  while a call is in flight (D7).

---

## 5. Detailed design

### 5.1 `NuGet.config` (two sources; example-specific mapping that diverges from root)
Declares `local-feed` → `./LocalFeed` and `nuget.org`. Uses `packageSourceMapping`
that **intentionally differs** from the repo root: `KyuzanInc.*` → **local-feed**
(one pattern covering **both** Peak *and* Turnkey, so the vendored Turnkey resolves
locally — the root config instead routes `KyuzanInc.Turnkey.*` → GitHub, which
would defeat D3), and `*` → nuget.org. **NFU caveat:** NFU's
`packageSourceMapping` support varies by version; if the pinned NFU (D12) does not
honor it, NFU probes all sources and Turnkey still resolves from the local feed —
so the mapping is a correctness aid, not the only guard. The plan verifies against
the pinned NFU.

### 5.2 `prepare-feed.sh` (no-auth setup, D3/D4)
Run from the example dir. Cleans stale feed entries, packs the **csproj** (not the
solution — packing the sln drags a Turnkey restore that wants GitHub auth), and
copies the **exact-pinned** Turnkey nupkg with an actionable cache-miss error:

```bash
#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
rm -f ./LocalFeed/KyuzanInc.Peak.Sdk.*.nupkg          # avoid resolving a stale SDK build
mkdir -p ./LocalFeed
# 1) pack the working-tree SDK (csproj only) into the local feed
dotnet pack ../../packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj -c Release -o ./LocalFeed
# 2) vendor the EXACT pinned transitive Turnkey nupkg from the NuGet cache (no GitHub auth at Unity restore)
TK_VER="$(grep -oE 'KyuzanInc\.Turnkey\.Sdk" Version="\[?[^]"]+' ../../Directory.Packages.props | grep -oE '[0-9].*')"
TK_NUPKG="${NUGET_PACKAGES:-$HOME/.nuget/packages}/kyuzaninc.turnkey.sdk/${TK_VER}/kyuzaninc.turnkey.sdk.${TK_VER}.nupkg"
[ -f "$TK_NUPKG" ] || { echo "ERROR: $TK_NUPKG not found. Run 'dotnet restore' on the solution once first (warms the cache)."; exit 1; }
cp "$TK_NUPKG" ./LocalFeed/
echo "LocalFeed ready: $(ls ./LocalFeed)"
```

README documents the one prerequisite (a prior `dotnet restore`, which every
contributor has done to build the repo) and the GitHub-auth alternative. (The
`TK_VER` extraction above is illustrative; the plan tests it against the real
`Directory.Packages.props` line `Version="[0.1.0-alpha.0]"` or simply hardcodes the
pinned exact version — either is fine since the pin is exact and rarely changes.)

### 5.3 `PeakExampleDemo.cs` (MonoBehaviour)
- `namespace Peak.Example;` (matches the asmdef root namespace — not namespace-free).
- `MonoBehaviour` + `OnGUI`; `[SerializeField] private string apiUrl = "https://api.peak.xyz";`
  (no `[SerializeField]` for the key — D9).
- State: `PeakClient? _client`, `AuthenticatedPeakClient? _authed`, `State _state`,
  `bool _busy`, buffers `_apiKey`, `_email`, `_otpCode`, `_pendingOtpId`, `_address`,
  `_log`. `PeakClientOptions.LoggerFactory` left null (D9).
- `Initialize()` → `PeakClient.Initialize(apiUrl, _apiKey)` in `try/catch (PeakError)`.
- Handlers `async void` with the D7 try/catch + `_busy` guard; no `ConfigureAwait(false)`.
- **Import**: `var init = await _authed.InitImportPrivateKeyAsync();` → `var bundle =
  PeakCrypto.EncryptPrivateKeyToBundle(new PeakCrypto.EncryptPrivateKeyToBundleParams
  { PrivateKey = _importKeyHex, ImportBundle = init.ImportBundle, OrganizationId =
  init.OrganizationId, UserId = init.UserId, KeyFormat = "HEXADECIMAL" });` → `await
  _authed.CompleteImportPrivateKeyAsync(bundle, ChainTypeEvm);` where
  `const string ChainTypeEvm = "evm";` (D6 — **not** `"ETHEREUM"`).
- **Export**: `var target = PeakCrypto.GenerateP256KeyPair();` → `var exp = await
  _authed.ExportPrivateKeyAsync(_address, target.PublicKey);` → `var key =
  PeakCrypto.DecryptExportBundle(new PeakCrypto.DecryptExportBundleParams { ExportBundle
  = exp.ExportBundle, EmbeddedKey = target.PrivateKey, OrganizationId =
  exp.OrganizationId, KeyFormat = "HEXADECIMAL" });` — `key` is a raw private key;
  it is **never** written to `_log`/`Debug` (D9), only a redacted marker.
- The import key is entered via a runtime field labeled **test-keys-only**.

### 5.4 `Peak.Example.asmdef`
References the NFU-restored DLLs. **Mechanism (corrected from r1):** an asmdef does
not enumerate `Assets/Packages` DLLs unless `overrideReferences:true` +
`precompiledReferences:[…]`; by default it sees a restored DLL **iff that DLL's
`.dll.meta` has Auto-Reference on** (NFU's default). So a plain asmdef
(`autoReferenced:true`, no engine excludes, `allowUnsafeCode:false`) usually
compiles. The deterministic fallback — if a restored DLL ships Auto-Reference off
or must be excluded — is `overrideReferences:true` + `precompiledReferences`; the
plan validates which is needed during the human smoke and pins it.

### 5.5 `ProjectSettings/` + Unity version + `manifest.json`
- Commit the **full standard `ProjectSettings/` set** (§4.1). In
  `ProjectSettings.asset` pin the IL2CPP-relevant values: `scriptingBackend =
  IL2CPP`, `apiCompatibilityLevel = .NET Standard 2.1` (so the netstandard2.1 SDK
  asset + STJ/`Microsoft.Extensions.*` resolve; a wrong value yields a
  `TypeLoadException` that would masquerade as a stripping failure — m4),
  `managedStrippingLevel = Minimal` (D11), `allowUnsafeCode` as required.
- `EditorBuildSettings.asset` lists `SampleScene` (else the Player build has no
  scene to run).
- `ProjectVersion.txt` pins a specific **6000.0.x LTS** (§9 OQ1, confirmed with the
  smoke runner).
- `manifest.json` pins NFU via git-URL UPM (D12) plus the default modules a 3D
  template needs.

### 5.6 IL2CPP smoke log (`docs/operations/il2cpp-smoke-<date>.md`)
Create `docs/operations/` + a templated log capturing: Unity version; target
platform(s) (**Standalone first; iOS/Android if the runner has the toolchains —
note the BouncyCastle risk is highest on mobile, R7**); scripting backend = IL2CPP;
**`managedStrippingLevel` actually used** (the conclusion is only valid for that
level); `apiCompatibilityLevel`; pass/fail per flow step; the post-`await`
main-thread-id assertion (D7); and any `link.xml` entry that proved load-bearing
or removable. Pre-seed the template with the D11 `link.xml` already in place.

### 5.7 Docs + plan + CLAUDE.md edits (enumerated; exact pre-edit strings — doc-drift discipline)
- **`CLAUDE.md`** (D10): under "Working with this repo", change the bullet
  *"`UnityEngine` outside `packages/peak-sdk-csharp-unity/`"* to scope the
  prohibition to **shipped SDK packages** and explicitly permit `examples/**`
  consumer samples to reference `UnityEngine`. (Quote the exact current bullet in
  the PR diff.)
- **`plans/plans-peak-sdk-csharp.md`**:
  - W3 status row — current text *"⬜ Deferred to v0.1.0-alpha.1"* is **stale**
    (alpha.1 **and** alpha.2 already shipped). Update to reflect the **Unity
    portion delivered** (Console + Godot still deferred), link this design + the
    smoke log, and correct the deferral target to the current in-flight version.
  - PR-3 narrative — the sentence *"intentionally minimal: just the asmdef + a
    single MonoBehaviour that calls `PeakClient.Initialize`"* is contradicted by
    D2; rewrite it to the full-flow scope and note the in-repo sample now realizes
    the C-E2 / OQ3 IL2CPP smoke.
- **`docs/development.md`**: add an "examples" pointer; reconcile the existing
  *"Unity 2021.2 LTS or newer, plus NuGetForUnity 4.x"* line with the 6000.0.x +
  pinned-NFU choice (§9 OQ1/OQ2); document the example's vendored-Turnkey
  no-auth local-feed path beside the existing "Unity consumers … still need the
  GitHub Packages source" guidance (D3 divergence).
- **`README.md`** (repo root): one-line pointer to the example.

---

## 6. Verification strategy (layered; honest about its ceiling)

| # | Layer | What it proves | Automated? |
| --- | --- | --- | --- |
| 1 | `dotnet restore --locked-mode` + `build -c Release` + `test --filter "Category!=E2E"` + `format --verify-no-changes` | adding a sibling `examples/` dir does not touch the solution graph/lock files | **CI** |
| 2 | `consumer-restore-check` (`csharp-ci.yml`) | `dotnet pack` (csproj) yields a consumable nupkg restorable from `./local-feed` | **CI** (existing) |
| 3 | **D13 net8.0 API-conformance shim** | the example's SDK-call expressions compile against the packed assembly — catches signature/nullability/params-shape drift (the r1 bug class) | **CI** (new) |
| 4 | Static review | every call matches §1.2 incl. the `chainType` value domain | human/agent |
| 5 | Editor Play-mode smoke | issue ACs 1–4 (open, OTP flow, List/Import/Export callable) | **human** (§9 OQ6 creds) |
| 6 | IL2CPP Player build + run | issue AC 5 (the primary purpose) → `docs/operations/il2cpp-smoke-<date>.md` | **human** |
| 7 | markdown link-lint on new docs | doc-drift discipline | CI |

**Blunt statement (R1):** layers 1–3 + 7 are automated but **none of them is a
Unity acceptance criterion**; **0 of issue #24's 5 ACs are CI-enforced**. The merge
gate is layer 3 (conformance compile) + layer 4 (static review) + the layer-6 human
smoke log. CI green ≠ smoke passed.

---

## 7. Risks

- **R1 — Over-claiming CI coverage.** 0/5 ACs are CI-gated. The PR description +
  plan state this; the merge gate is the human smoke log + the D13 conformance
  compile, not CI green.
- **R2 — Transitive Turnkey not resolvable → first-restore failure.** Mitigated by
  D3 (vendor the exact-pinned nupkg) + D8 (`packages.config` enumerates the closure)
  + the `prepare-feed.sh` cache-miss error + the README prerequisite.
- **R3 — NFU version skew / `packageSourceMapping`.** Mitigated by pinning NFU
  (D12) and keeping Turnkey on the local feed so an unmapped multi-source probe
  still resolves it (§5.1).
- **R4 — Unity version drift (`ProjectVersion.txt`).** Mitigated by OQ1 + a
  README-documented supported range.
- **R5 — Key material in logs/UI.** The SDK logs request bodies (OTP code, **import
  bundle = wrapped key**, signed envelopes) at `Debug`; `DecryptExportBundle`
  returns a raw private key. Mitigated by D9: `NullLogger` default (makes the
  `Debug` logging inert), never echoing raw key material/bodies to UI/`Debug`, a
  test-keys-only warning, and the no-`[SerializeField]`-key rule.
- **R6 — `.gitignore` gaps commit Unity churn or a key-bearing scene.** Mitigated
  by D5 (official `Unity.gitignore` base) + D9 (committed scene has no key) + a
  pre-merge `git status`/`git diff` check that only the intended skeleton is staged
  and the scene's serialized fields are key-free.
- **R7 — IL2CPP strips BouncyCastle (the smoke's point, and the *expected* mobile
  outcome).** `BouncyCastle.Cryptography` has documented IL2CPP UnityLinker
  failures under stripping. Mitigated by D11 (ship a conservative `link.xml`
  preserving BouncyCastle + Turnkey; explicit `managedStrippingLevel`). A
  stripping failure is a **successful** smoke finding; the `link.xml` delta is
  recorded in the log, not hidden.
- **R8 — `async void`/`ConfigureAwait` main-thread violation.** The SDK uses
  `ConfigureAwait(false)` internally. Mitigated by D7 (no `ConfigureAwait(false)`
  in handlers; main-thread-id assertion in the smoke).
- **R9 — `[SerializeField]` API-key leak.** Eliminated by D9 (key never serialized;
  committed scene key-free; pre-merge diff check).

---

## 8. Implementation outline (hand-off to the plan)

1. Scaffold `examples/peak-sdk-unity-reference/` (§4.1) with the official
   `Unity.gitignore` base (D5) and the full committed `ProjectSettings/` set (§5.5).
2. `NuGet.config` (§5.1) + `prepare-feed.sh` (§5.2) + `Assets/packages.config`
   (D8) + `Assets/link.xml` (D11) + `Packages/manifest.json` (D12); README
   (no-auth default + GitHub-auth alt + test-creds warning + run steps).
3. `PeakExampleDemo.cs` against §1.2/D6 (the `ChainTypeEvm` constant; the §4.3
   state machine + Import→List→Export ordering), D7 (`async void`/try-catch/no
   `ConfigureAwait`/`_busy`), D9 (no serialized key, redacted logging,
   `NullLogger`); the `Peak.Example.asmdef` (§5.4); a key-free `SampleScene.unity`
   wired into `EditorBuildSettings`; all `.meta` files.
4. `docs/operations/` + the IL2CPP smoke-log template with the D11 `link.xml`
   pre-seeded (§5.6).
5. The D13 net8.0 API-conformance shim + its CI wiring.
6. Docs/plan/`CLAUDE.md` edits with exact pre-edit strings (§5.7).
7. Verify layers 1–3 + 7 in CI/locally; hand layers 5–6 (Editor + IL2CPP smoke)
   to the human runner and capture the log.

## 9. Open questions

- **OQ1 — Exact Unity 6000.0.x pin** (smoke runner's installed LTS). *Rec:* latest
  6000.0 LTS on their machine; record in `ProjectVersion.txt` + the smoke log.
  Blocking for the human smoke, not for authoring.
- **OQ2 — Exact NFU tag** within the documented 4.x baseline, via the D12 git-URL.
  *Rec:* a recent 4.x tag known to honor a local-path source; the plan pins + verifies.
- **OQ3 — Import/export depth in v1.** *Rec:* wire the real calls (they exercise
  BouncyCastle under AOT — the point) behind a test-key field with a warning; a
  full live-wallet round-trip is a stretch goal of the human smoke, not a merge
  blocker.
- **OQ4 — `.gitignore` scope.** *Rec:* a local
  `examples/peak-sdk-unity-reference/.gitignore` (official Unity base) keeps Unity
  noise out of the root file. (Resolved toward local; the §4.1 tree assumes it.)
- **OQ5 — D13 shim placement** (a `tests/` conformance project vs an
  `examples/.../conformance/` one) + CI gating. *Rec:* a small dedicated net8.0
  project the CI builds; the plan picks.
- **OQ6 — Smoke credentials provenance (blocking for the human smoke).** Where does
  the runner get the Peak environment base URL, a valid `projectApiKey`, and a
  test account/key material for import/export? The `apiUrl` default
  `https://api.peak.xyz` is the **real** API; the prose must not assume a separate
  "test environment" exists without naming it. *Rec:* a README prerequisite naming
  the environment + how to obtain a test `projectApiKey` and test keys; reconcile
  the prod default with the test-credentials warning.

## 10. Acceptance-criteria mapping (issue #24)

| Issue #24 acceptance criterion | How satisfied | Enforced by |
| --- | --- | --- |
| `dotnet pack` produces `.nupkg`; Unity restores via local feed | D1/D3/D4/D8, `prepare-feed.sh` (§5.2), `NuGet.config` (§5.1) | Layer 2 (CI pack) + **human** restore |
| Opens in Unity 6000.0.x without errors | §4.1 skeleton + full `ProjectSettings/` + scene (D5/§5.5) + `packages.config` (D8) | **human** (OQ1) |
| Full OTP flow end-to-end in Editor Play mode | §4.3 state machine; §1.2/D6 surface; D7 | **human** (OQ6) |
| `ListAccountsAsync`, Import (`Init`/`Complete`), `ExportPrivateKeyAsync` callable | §4.3 Authenticated screen; §5.3 (Import→List→Export ordering) | **human** |
| IL2CPP Standalone build succeeds (`docs/operations/il2cpp-smoke-<date>.md`) | §5.6 log; `docs/operations/` created; D11 `link.xml` | **human** (primary purpose) |

**Honesty note (R1):** 0/5 ACs are CI-enforced. The merge gate is the D13
conformance compile (layer 3) + static review (layer 4) + the human smoke log
(layer 6) — not CI green.

## 11. Out-of-scope follow-ups (noted, not done)
- Mobile (iOS/Android) IL2CPP smoke if the runner lacks the toolchains (do
  Standalone now; track mobile as a follow-up — the BouncyCastle risk is highest
  there, R7).
- Console + Godot W3 examples (deferred).
- A redaction follow-up for the SDK's `Debug` request-body logging (pre-existing;
  2026-06-03 design R7) — the example sidesteps it via `NullLogger` (D9) but the
  SDK-side fix is separate.

## 12. References
- Issue: [KyuzanInc/peak-sdk-csharp#24](https://github.com/KyuzanInc/peak-sdk-csharp/issues/24).
- Verified surface (read 2026-06-09): `packages/peak-sdk-csharp/src/{PeakClient.cs,
  AuthenticatedPeakClient.cs,PeakCrypto.cs,PeakError.cs,Models/Models.cs,
  Services/PrivateKeyService.cs}` (the `chainType ∈ {evm,solana}` switch),
  `Serialization/PeakResponseJson.cs` + `Utils/DefaultPeakHttpClient.cs` +
  `PeakJsonContext.cs` (STJ source-gen, no reflection fallback — D11),
  `Services/{AuthService,AccountService}.cs` (`ConfigureAwait(false)` — D7/R8).
- Packaging/CI: `peak-sdk-csharp.csproj`, `Directory.Packages.props`, repo
  `nuget.config`, `docs/development.md`, `.github/workflows/{csharp-ci.yml
  (consumer-restore-check),consumer-smoke.yml}`.
- Plan/decisions: `plans/plans-peak-sdk-csharp.md` (W3 row, PR-3 narrative, C-E2);
  Decision D25 (Console/Godot deferred, Unity-only, standard Task — external peak
  monorepo plan).
- Prior design (IL2CPP / STJ-only; the OQ3 this realizes):
  `docs/superpowers/specs/2026-06-03-unity-il2cpp-stj-only-consumer-path-design.md`.
- `CLAUDE.md` (the `UnityEngine`-outside-adapter rule edited by D10; the
  crypto-no-source-vendor rule, satisfied by D3 binary consumption).
- External domain facts: GitHub `Unity.gitignore`; NuGetForUnity (GlitchEnzo /
  OpenUPM `com.github-glitchenzo.nugetforunity`); `BouncyCastle.Cryptography` IL2CPP
  UnityLinker stripping reports; Unity IL2CPP bytecode-stripping / `link.xml` docs.

## 13. Strongest / weakest (self-assessment)
- **Strongest:** §1.2 surface corrections (incl. the `chainType` domain) and §1.3
  transitive-closure analysis — both catch real defects in the issue and are
  source-verified.
- **Weakest:** the human-only ceiling on the Unity ACs (R1). D13 adds the one cheap
  automated floor; everything past "does it compile against the package" still
  rides on the human IL2CPP smoke, which is intrinsic to a no-Unity-CI repo.

## 14. Change log
- **2026-06-11 r4 (decision reversal — internal-testing default switched to GitHub
  Packages)** — per the maintainer, internal verification consumes
  `KyuzanInc.Peak.Sdk` + `KyuzanInc.Turnkey.Sdk` directly from **GitHub Packages**
  (`github-kyuzan`), not a local `.nupkg` feed. This **supersedes the no-auth
  local-feed design** (D1/D3/D4, `prepare-feed.sh`, the `../LocalFeed` source, and
  the r3 no-auth proof): `prepare-feed.sh` is deleted, `Assets/NuGet.config`
  declares the `github-kyuzan` source (its `read:packages` credential injected from
  the machine-global `~/.nuget` config per `docs/development.md`), and the
  `/LocalFeed/` gitignore line is removed. Rationale: `KyuzanInc.Peak.Sdk` will be
  published (eventually to nuget.org), at which point the `github-kyuzan` source can
  be dropped and restore needs no auth at all; until then GitHub Packages is simpler
  than maintaining the local-feed plumbing. Re-verified in Unity 6000.0.73f1 via the
  Editor CLI: restore pulls all 21 packages from `github-kyuzan` + nuget.org and
  `Peak.Example.dll` compiles with 0 errors.
- **2026-06-11 r3 (as-built, validated in Unity 6000.0.73f1 via the Editor CLI)** —
  ran the project headless (`Unity -batchmode`) and corrected two defects the
  on-paper design missed: (1) **NuGet.config location.** NuGetForUnity reads
  `Assets/NuGet.config` (and injects the machine's *global* NuGet config), NOT a
  project-root `NuGet.config`. The r2 design/plan put the local-feed config at the
  project root, so the "no GitHub Packages auth" path silently relied on a global
  `github-kyuzan` source — broken for a fresh/no-auth checkout. Fixed: the
  local-feed config lives at `Assets/NuGet.config` (`../LocalFeed`), the root
  `NuGet.config` is removed, and a clean restore now pulls all 21 packages from the
  local feed + nuget.org. (2) **ProjectSettings seed does not survive
  normalization.** Unity rewrites a hand-authored `ProjectSettings.asset` on first
  open and drops the per-platform `scriptingBackend`/`managedStrippingLevel` maps
  (resetting Standalone to Mono). Fixed: set IL2CPP + Low stripping via the
  `PlayerSettings` API and committed Unity's normalized full `ProjectSettings/` set
  (apiCompatibilityLevel stayed `.NET Standard 2.1`). Also: pinned
  `ProjectVersion.txt` to **6000.0.73f1**, confirmed NuGetForUnity **v4.4.0**
  resolves, committed `Packages/packages-lock.json`, and recorded the Editor-compile
  PASS (0 errors, `Peak.Example.dll` built) in the smoke log. The IL2CPP Player
  build + runtime flow remain human (smoke log = PARTIAL). NFU auto-restore needs a
  clean compile first (its `[InitializeOnLoad]` never fires while the example script
  fails on the not-yet-restored SDK), so the CLI flow restores with the script
  briefly moved aside, then compiles.
- **2026-06-09 r2** — folded one multi-aspect round (4 lens agents + Codex; r1 was
  NOT-READY). Fixed **4 BLOCKERs**: (B1) added `Assets/packages.config` enumerating
  the full closure since NFU is not transitive (D8); (B2) commit the full standard
  `ProjectSettings/` set + a key-free `SampleScene.unity` wired into
  `EditorBuildSettings`, with explicit IL2CPP/`apiCompatibilityLevel`/stripping
  values (D5/§5.5) instead of a "minimal" hand-written settings file; (B3) the
  `chainType` `"ETHEREUM"` → `"evm"` runtime bug, with the value domain pinned
  normative (D6); (B4) the `[SerializeField] projectApiKey` scene-leak → key never
  serialized, `NullLogger` default, no key/body echoing (D9). Fixed MAJORs: CI
  job mis-citation (`consumer-smoke.yml` → `consumer-restore-check`); `prepare-feed.sh`
  packs the csproj not the sln, exact-pinned Turnkey copy (D3/§5.2); asmdef↔NFU
  reference mechanism (§5.4); BouncyCastle IL2CPP risk + a shipped `link.xml`
  (D11); `async void`/`ConfigureAwait(false)` main-thread note + `_busy`
  single-flight (D7/R8); NFU pin made deterministic (D12); the `CLAUDE.md`
  `UnityEngine` carve-out (D10); the stale W3 "Deferred to alpha.1" row (§5.7); a
  net8.0 API-conformance compile floor (D13); the blunt 0/5-ACs-CI honesty note
  (§6/R1); and the smoke-credentials open question (OQ6). Minors folded: second-order
  transitives, Import→List→Export ordering, official `Unity.gitignore` base, the
  `Peak.Example` namespace, and exact-pre-edit-string doc edits (§5.7). A round-2
  confirmation pass (Codex + the Unity and completeness/security lenses) returned
  READY / READY / READY-WITH-FIXES; folded its one carried MAJOR — `packages.config`
  must enumerate the **complete** ~20-package graph (the first-order six plus the
  dozen-plus second-order runtime packages the lock file pulls, incl.
  `System.Numerics.Vectors`), generated mechanically from `packages.lock.json`
  (D8) — plus the `prepare-feed.sh` version-extraction note (§5.2).
- **2026-06-09 r1** — initial draft against the verified `0.1.0-alpha.2` surface;
  corrected the issue's API table (§1.2), surfaced the transitive-Turnkey no-auth
  gap (D3), recorded the scope widening (D2), made the human-vs-CI boundary
  explicit. Bounced NOT-READY (missing `packages.config`/ProjectSettings/scene;
  `"ETHEREUM"` chainType; `[SerializeField]` key leak; CI mis-citation).
</content>
