# Design — Unity / IL2CPP-consumable SDK: a System.Text.Json-only consumer runtime path

- **Date:** 2026-06-03
- **Status:** Draft — r4, plan-grade (cleared three multi-aspect + Codex review
  rounds; the r3 Codex re-review found one BLOCKER + one MAJOR + one MINOR in
  the **test/CI design** — the contract-test field-set comparison, the
  internalization-test deletion, and the external-consumer STJ smoke — all fixed
  in r4 below; the core Option C decision was unaffected). See the change log in
  Section 13.
- **Issue:** [KyuzanInc/peak-sdk-csharp#18](https://github.com/KyuzanInc/peak-sdk-csharp/issues/18)
- **Topic:** Remove Newtonsoft.Json and the embedded RestSharp generated client
  (`KyuzanInc.Peak.PublicApiClient`) from the `KyuzanInc.Peak.Sdk` **consumer
  runtime path**. Deserialize the authenticated/wallet responses directly into
  the SDK's public System.Text.Json (STJ) source-generated DTOs; pin
  `System.Text.Json` to **8.0.x**; add a Unity-shaped consumer assertion to CI.
- **Supersedes (for the response (de)serialization layer only):**
  [docs/superpowers/specs/2026-06-01-consume-generated-openapi-client-design.md](2026-06-01-consume-generated-openapi-client-design.md)
  — the "Option 1 / Newtonsoft response DTOs" decision landed by
  [#14](https://github.com/KyuzanInc/peak-sdk-csharp/pull/14). The generated
  client itself, its codegen pipeline, and the `openapi-client-drift` CI job are
  **kept**; only its place on the SDK's runtime/package path is removed.
- **Tracks:** W5 (`peak-sdk-unity` thin-adapter migration) in
  [KyuzanInc/peak#330](https://github.com/KyuzanInc/peak/pull/330). The Unity
  migration is paused pending this fix.

---

## 1. Context

### 1.1 What the SDK does today (verified against source, post-#14)

`KyuzanInc.Peak.Sdk` hand-rolls its HTTP layer (`PeakClient` → services →
`IPeakHttpClient`) and serializes its **request** bodies with STJ source
generation (`PeakJsonContext`). That request path is already AOT/IL2CPP-safe and
is **not** changed by this design.

PR #14 changed the **response** path. Today every authenticated/wallet endpoint
deserializes its response into a **generated** DTO from the internal
`KyuzanInc.Peak.PublicApiClient` assembly using **Newtonsoft.Json**, then maps it
to the public DTO:

- `packages/peak-sdk-csharp/src/Serialization/PeakResponseJson.cs:24-35` —
  `Deserialize<T>` routes any `T` from the generated-client assembly through
  `Newtonsoft.Json.JsonConvert.DeserializeObject<T>(body, GeneratedClientSettings)`
  (with a `TolerantEnumContractResolver`), and only otherwise through STJ.
- `packages/peak-sdk-csharp/src/Utils/DefaultPeakHttpClient.cs:136,150` — the
  transport calls `PeakResponseJson.Deserialize<T>` and catches **both**
  `System.Text.Json.JsonException` and `Newtonsoft.Json.JsonException`.
  (Issue #18 cites a stale `Http/DefaultPeakHttpClient.cs` path; the file is at
  `Utils/`. This spec uses the real path throughout.)
- `packages/peak-sdk-csharp/src/Services/AuthService.cs:45,70`,
  `AccountService.cs:34,45,59,71`, `PrivateKeyService.cs:68,132,209` — every
  call uses a `Gen.*ResponseDto` response type (`Gen = KyuzanInc.Peak.PublicApiClient.Model`)
  and `dto?.ToPublic()` (`packages/peak-sdk-csharp/src/Mapping/GeneratedDtoMappers.cs`).
- `packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj:37,41,51-62` — the SDK
  declares a direct `Newtonsoft.Json` and `System.ComponentModel.Annotations`
  dependency, references the generated client `PrivateAssets="all"`, and embeds
  `KyuzanInc.Peak.PublicApiClient.dll` into the nupkg via the
  `_IncludeGeneratedClientDll` target.
- `Directory.Packages.props:24` — `System.Text.Json` is pinned to **10.0.8**
  (a .NET 10-wave package); `:38,:42` add `Newtonsoft.Json 13.0.3` and
  `System.ComponentModel.Annotations 5.0.0`.

### 1.2 Why this blocks Unity / IL2CPP (issue #18, confirmed)

A NuGetForUnity consumer of `KyuzanInc.Peak.Sdk` (the `peak-sdk-unity` thin
adapter, W5) hits four problems, all on the **response** path #14 introduced:

1. **Newtonsoft collision.** The SDK now declares `Newtonsoft.Json 13.0.3`
   directly. Unity projects already ship `com.unity.nuget.newtonsoft-json`
   (3.2.1 = Newtonsoft 13.0.x) → a duplicate `Newtonsoft.Json.dll` assembly
   conflict.
2. **Embedded RestSharp client in the package closure.** The nupkg ships
   `KyuzanInc.Peak.PublicApiClient.dll`, which references RestSharp 112 + Polly
   8.1 (held by `VersionOverride`, hidden from the dependency graph by
   `PrivateAssets="all"`). Even though the SDK only touches its `Model.*` types,
   the DLL is on the consumer's runtime closure and is IL2CPP-fragile (RestSharp
   reflection). NuGetForUnity surfaces the DLL regardless of `PrivateAssets`.
3. **Newtonsoft reflection under IL2CPP.** The generated DTOs are deserialized
   by Newtonsoft via runtime reflection; IL2CPP AOT stripping makes this
   fragile and would require a `link.xml` workaround (recorded as OQ2/R1 of the
   #14 design, never delivered — no Unity adapter package exists in this repo).
4. **`System.Text.Json 10.0.8`.** A .NET 10-wave package maximizes restore risk
   on Unity 6000.0 / Mono / IL2CPP. `KyuzanInc.Turnkey.Sdk` (already Unity-proven)
   transitively pulls **8.0.5**; the two should converge on 8.0.x. (This is a
   restore-alignment move, **not** a security fix — see Section 6.4: both 10.0.8
   and 8.0.5 are non-vulnerable builds.)

### 1.3 Facts that make the fix small

- **The public DTOs are already STJ-registered.** `PeakJsonContext`
  (`packages/peak-sdk-csharp/src/PeakJsonContext.cs:20-34`) registers every
  public response wrapper and entity the endpoints return
  (`InitOtpLoginResponse`, `CompleteOtpLoginResponse`, `ListAccountsResponse`,
  `ListAccountAddressesResponse`, `GetAddressDetailResponse`,
  `CompleteImportPrivateKeyResponse`, `InitImportPrivateKeyResponse`,
  `ExportPrivateKeyResponse`, plus `AccountResponse(/[])`,
  `AccountAddressResponse(/[])`, `UserResponse`, `AccountSourceResponse`). These
  were the **pre-#14** deserialization targets and remain registered. Direct STJ
  deserialization into them works with **one** new registration (Section 6.1).
- **The public DTOs read unknown enum values more leniently than the generated
  ones.** Every enum-ish field on the public DTOs is `string?`
  (`Models.cs:26-27,37,46-47` — `ChainType`, `BitcoinAddressType`,
  `SourceType`, `CreationMethod`, `DeletionStatus`). STJ reads any string value
  straight through, so the additive-server-enum hazard the #14 design spent a
  whole section mitigating (`TolerantEnumContractResolver`) disappears.
  **Behavior delta to record:** an unknown `chainType` (e.g. `"aptos"`) now
  surfaces as the **raw string** `"aptos"`, not the resolver's `null` and not a
  hard-fail; a **non-string** enum token (e.g. `chainType: 1`) now **fails
  closed** as `InvalidResponse` (STJ rejects the wrong token type) rather than
  being softened to `null`. This leniency is bounded where it matters: the only
  closed-set dispatch on a **response-derived** enum string is
  `ExportPrivateKeyAsync`'s `sourceType` branch (`PrivateKeyService.cs:171/:189/:206`,
  throws `InvalidResponse` on an unknown `sourceType`).
  `CompleteImportPrivateKeyAsync`'s `chainType` switch (`:100/:110`) dispatches on
  a **caller-supplied argument**, not a deserialized field, so the passthrough
  change does not touch it.
- **STJ defaults are a safe wire posture.** `PeakJsonContext` sets no
  `UnmappedMemberHandling`, no `ReferenceHandler`, no custom converters and no
  `[JsonExtensionData]`: unknown JSON members are silently ignored, `MaxDepth`
  is the default 64, and there is no polymorphic/`$type` surface — so no STJ
  deserialization-gadget or DoS surface beyond the (patched) CVEs in Section 6.4.
- **Spec-drift detection does not depend on the runtime path.** The
  `openapi-client-drift` CI job (`.github/workflows/csharp-ci.yml:213-247`)
  regenerates the client from the pinned spec and fails on any diff. It runs
  whether or not the SDK consumes the client at runtime, so spec→client drift
  stays covered for free (Section 6.7).

---

## 2. Goals and non-goals

### Goals

- A NuGetForUnity / IL2CPP consumer of `KyuzanInc.Peak.Sdk` can run the full
  authenticated/wallet flow with **no RestSharp, Polly, or Newtonsoft** on its
  runtime closure and **no `TypeLoadException` / `FileNotFoundException`**.
- The consumer **(de)serialization is 100% STJ source-gen** (`PeakJsonContext`),
  with the existing "throw if a type is not registered" guarantee (no reflection
  fallback) intact.
- `System.Text.Json` converges on **8.0.x** (aligned with
  `KyuzanInc.Turnkey.Sdk`).
- The **public API surface is unchanged** (the invariant
  `PublicSurfaceBaselineTests` stays green), and the **request wire-format**
  (signed Turnkey envelopes) **is unchanged** (`TurnkeyWireFormatSmokeTests`
  stays green).
- All non-E2E tests stay green.

### Non-goals

- **No new endpoints.** The Auth/Account/PrivateKey surface is unchanged.
- **No change to the request path** or the signed-envelope construction
  (`PrivateKeyService` Turnkey stamps stay byte-for-byte; Section 6.6).
- **No `[JsonRequired]` hardening of the public DTOs.** `Models.cs` stays
  all-nullable; required-field validation is intentionally relaxed to the
  pre-#14 contract (Section 6.3 makes this an explicit, signed-off decision).
- **No deletion of the generated client project or its codegen.** It remains
  `IsPackable=false`, build-time, internalized, and drift-checked. Only its SDK
  **runtime / package** reference is removed.
- **No Unity adapter / `link.xml`.** No `peak-sdk-csharp-unity` package exists in
  this repo; the Unity thin adapter lives in `peak-sdk-unity` (W5) and is out of
  scope. The achievable proxy here is a CI dependency-graph + STJ-resolution
  assertion (Section 6.9).
- **No full Unity 6000.0 IL2CPP runtime smoke in CI.** A licensed Unity runner
  is out of scope; documented as a follow-up that must run in `peak-sdk-unity`
  (W5) before issue #18 AC1 is fully closed (Section 10 OQ3, Section 11).
- **No nuget.org publishing** (separate workstream).
- **Out of scope but noted:** `DefaultPeakHttpClient.cs:71` logs the full
  serialized request body at `Debug` (OTP code, signed envelopes). Pre-existing;
  a redaction follow-up is recommended (Section 6.6 / R7), not done here.

---

## 3. The trade-off this reverses, and how it is repaid

PR #14 deliberately brought Newtonsoft back into the SDK runtime (its design
Section 4.2) to gain one thing: the SDK's **response DTO shapes become
compile-time-bound to the generated client**, so spec drift surfaces as a build
break. #14 optimized for that binding without weighting the known W5/Unity
consumer; issue #18 re-prioritizes Unity-consumability, so this design is a
**deliberate correction**, not a flip-flop. It reverts the response
(de)serialization layer to hand-written STJ DTOs (the pre-#14 state) and repays
the two drift signals #14 provided:

1. **Spec → generated-client drift: unchanged.** `openapi-client-drift` already
   regenerates and diffs the client on every push/PR; this design keeps the
   client project and that job (`csharp-ci.yml:213`).
2. **Generated-client → hand-DTO drift: a P0 contract test, weaker than #14's
   compile-time binding (Section 6.7).** A `Category=Contract` test (test-only,
   never shipped) asserts that **every generated-DTO field is modelled by the
   public DTO** (a generated-⊆-public coverage check), and that the public DTO
   introduces no field outside a small, explicit **allowlist of legacy
   public-only fields** (currently exactly `UserResponse.IsAuthenticated`, which
   has no server-spec source — see `GeneratedDtoMappers.cs:42-50`). This is
   **runtime (CI), not compile-time**, and it does **not** assert required-ness
   (which is intentionally relaxed, Section 6.3).
   It is therefore a materially weaker signal than #14's build break — the spec
   does not claim equivalence — but it is the only guard left for hand-DTO drift
   once the compile binding is gone, so it is P0, not optional.

Net: consumers get an STJ-only package; maintainers keep the spec→client drift
signal intact and a (weaker, CI-time) client→DTO signal.

---

## 4. Decision

**Adopt issue #18 Option C: make the consumer runtime path STJ-only.**
Deserialize each authenticated/wallet response directly into the SDK's public
STJ DTOs via `PeakJsonContext`; delete the Newtonsoft response path, the
generated-DTO mappers, and the tolerant-enum resolver; remove the embedded
generated client and the `Newtonsoft.Json` / `System.ComponentModel.Annotations`
dependencies from the package; pin `System.Text.Json` to 8.0.5. Keep the
generated client build-time + drift-checked, add a P0 contract test, and add a
CI dependency-graph + STJ-resolution assertion.

### 4.1 Alternatives considered

- **A0 — keep #14's Newtonsoft path, add only a Unity `link.xml`.** Rejected:
  the link.xml lives in a Unity adapter that does not exist in this repo, does
  not remove the Newtonsoft collision (problem 1) or the embedded RestSharp DLL
  (problem 2), and leaves STJ at 10.0.8 (problem 4). Does not satisfy the
  acceptance criteria.
- **B — regenerate the client as STJ models-only and register those.** The #14
  design proved this is not achievable at the pinned generator core 7.9.0
  (its Section 3) without a generator-core bump. Out of scope and higher risk
  than reusing the public DTOs that already exist and are already registered.
- **C — STJ-only consumer path (chosen).** Smallest, lowest-risk: the public
  DTOs already exist (`Models.cs`) and are already STJ-registered
  (`PeakJsonContext`); the request path is already STJ. The change is mostly
  deletion + retyping service generics. An independent reviewer implemented C
  against this repo's .NET build/test/pack path during review (IL2CPP runtime
  *execution* deferred to W5 per §11/OQ3): clean build under
  `TreatWarningsAsErrors`, `dotnet format` clean, 52/52 non-E2E tests pass
  (incl. `TurnkeyWireFormatSmokeTests` + `PublicSurfaceBaselineTests`),
  locked-mode restore green, and the packed nupkg ships only
  `KyuzanInc.Peak.Sdk.dll` with no Newtonsoft/RestSharp/Annotations nuspec
  dependency — so the approach is validated, and the rest of this design fixes
  the prescription details that validation surfaced.

---

## 5. Architecture

**Before (post-#14):**

```
response body ──► PeakResponseJson.Deserialize<Gen.*Dto>
                    └─ Newtonsoft + TolerantEnumContractResolver
                       └─ generated Model DTO ──ToPublic()──► public STJ DTO ──► caller
   package ships: KyuzanInc.Peak.PublicApiClient.dll (RestSharp+Polly+Newtonsoft)
   declared deps: Newtonsoft.Json, System.ComponentModel.Annotations, System.Text.Json 10.0.8
```

**After (Option C):**

```
response body ──► STJ (PeakJsonContext, source-gen) ──► public STJ DTO ──► caller
   package ships: KyuzanInc.Peak.Sdk.dll only
   declared deps: System.Text.Json 8.0.5, Microsoft.Extensions.{Http,Logging.Abstractions}, KyuzanInc.Turnkey.Sdk
                  (no DECLARED Newtonsoft / System.ComponentModel.Annotations dependency; no RestSharp/Polly anywhere)
```

Note on the closure: `System.ComponentModel.Annotations` is no longer a
**declared** nuspec dependency or a direct `PackageReference`, but it still
appears as a **benign transitive** of `System.Text.Json 8.0.5` on the
`netstandard2.1` TFM. That is fine — it is not the Newtonsoft/RestSharp problem,
and Section 6.9 asserts on the layer that actually matters (no declared
Newtonsoft and no RestSharp/Polly/Newtonsoft anywhere in the graph). The request
path (STJ serialize via `PeakJsonContext`), the exact `application/json`
content type, headers, and the `PeakError` mapping are all unchanged.

---

## 6. Detailed design

### 6.1 Response path → direct STJ into public DTOs

Retype each service call's response generic from `Gen.*ResponseDto` to the
public DTO and drop the `.ToPublic()` hop and the `Gen` / `Mapping` usings.

- `AuthService.InitOtpLoginAsync` → `PostAsync<InitOtpLoginRequest, InitOtpLoginResponse>`; return the DTO directly.
- `AuthService.CompleteOtpLoginAsync` → `PostAsync<..., CompleteOtpLoginResponse>`; build `CompleteOtpLoginResult` from it (as today, minus the map).
- `AccountService.ListAccountsAsync` → `GetAsync<ListAccountsResponse>`; `dto?.Accounts ?? Array.Empty<AccountResponse>()`.
- `AccountService.ListAccountAddressesAsync` → `GetAsync<ListAccountAddressesResponse>`.
- `AccountService.UpdateAccountDisplayNameAsync` → `PostAsync<..., UpdateAccountDisplayNameEnvelope>`; return `dto?.Account`.
- `AccountService.GetAddressDetailAsync` → `GetAsync<GetAddressDetailResponse>`.
- `PrivateKeyService` init/complete/export → `InitImportPrivateKeyResponse` / `CompleteImportPrivateKeyResponse` / `ExportPrivateKeyResponse`.

**Ordering dependency:** `GetAddressDetailAsync` feeds
`PrivateKeyService.ExportPrivateKeyAsync` (`PrivateKeyService.cs:161`), so retype
both in the same step; the `internal GetAddressDetailResponse?` /
`AccountResponse?` signatures are unchanged, so callers are unaffected.

**One new internal type.** The server wraps update-display-name as
`{"account":{...}}`, and there is no public type with just an `Account` field
today (the public method returns `AccountResponse?`). Add an **internal** wrapper
so the public surface is unchanged. Name it `UpdateAccountDisplayNameEnvelope`
(deliberately **not** `UpdateAccountDisplayNameResponse`, to avoid a one-token
near-collision with the generated `UpdateAccountDisplayNameResponseDto`):

```csharp
// internal — does not touch the public surface
internal sealed class UpdateAccountDisplayNameEnvelope
{
    public AccountResponse? Account { get; set; }
}
```

and register it: `[JsonSerializable(typeof(UpdateAccountDisplayNameEnvelope))]`.
STJ source-gen supports an `internal` type in the `internal` `PeakJsonContext`,
so this adds no public API. (Alternatives — reading the existing registered
`Dictionary<string, object?>` escape valve, or a generically-named shared
wrapper — were considered; a single named internal type is the clearest and is
what the design chooses.)

### 6.2 Transport: STJ-only deserialization

Simplify `PeakResponseJson.Deserialize<T>` to the STJ-only form (drop the
generated-assembly branch, the `GeneratedClientSettings` field, **and the now-
orphaned `using System.Reflection;` and Newtonsoft usings** — `TreatWarningsAsErrors`
+ `dotnet format --verify-no-changes` would otherwise fail). Keep the
"not registered → `PeakError`" guard so AOT safety stays explicit:

```csharp
internal static T? Deserialize<T>(string body) where T : class
{
    var typeInfo = (JsonTypeInfo<T>?)PeakJsonContext.Default.GetTypeInfo(typeof(T))
        ?? throw new PeakError(PeakErrorCode.InvalidArgument,
            $"Type {typeof(T).Name} is not registered in PeakJsonContext.");
    return JsonSerializer.Deserialize(body, typeInfo);
}
```

(If OQ1 prefers inlining this back into the transport, delete the file entirely
and the orphaned-using point is moot.) In `DefaultPeakHttpClient.SendAsync`, the
parse-error catch drops the Newtonsoft arm and becomes `catch (JsonException ex)`
again (`DefaultPeakHttpClient.cs:150`); the empty-body short-circuit
(`:134 if (string.IsNullOrEmpty(body)) return null;`) and the per-caller null-DTO
handling are **retained** (a JSON `null` literal also yields a null DTO — the
same already-tested path). The request-path usings (`System.Text.Json`,
`…Metadata`) stay in use by `PostAsync`, so no SDK using is orphaned there.
Remove the Newtonsoft remark from `IPeakHttpClient` (`IPeakHttpClient.cs:14-21`)
— it is an XML doc comment, so it has no effect on the public-surface check
(Section 6.10).

### 6.3 Required-field validation policy (explicit, accepted behavior change)

This revert is **not** behavior-neutral at the validation layer, and the design
makes the change explicit rather than implicit. The generated DTOs decorate
nearly every field with `[DataMember(IsRequired = true)]` (e.g.
`AccountResponseDto` `id/userId/accountSourceId/accountIndex/originProjectId`,
and `CompleteOtpLoginResponseDto` `sessionJwt/isNewUser`), and Newtonsoft
**hard-fails** a missing required field (→ `InvalidResponse` today). The public STJ DTOs (`Models.cs`) are all-nullable and not
`[JsonRequired]`, so STJ **silently** deserializes a missing field to its default
(`accountIndex` → `0`, `id` → `null`).

**Decision: accept the loosening (Section 2 non-goal: no `[JsonRequired]`).**
Rationale:

- It **restores the pre-#14 contract**. These DTOs were all-nullable and
  lenient for the SDK's whole shipped life before #14; #14's strictness was an
  incidental side effect of borrowing the generated DTOs, not a designed
  guarantee.
- The SDK already **fails closed on every response field it uses for control
  flow**: `InitImport`/`Export` reject an empty `ImportBundle`/`ExportBundle`
  (→ `TurnkeyError`), and `ExportPrivateKeyAsync` rejects a missing
  `AccountSource` / `TurnkeyResourceId` (→ `InvalidResponse`,
  `PrivateKeyService.cs:162-174`). The fields that would now default silently
  (`accountIndex`, `id` on a listed account) are pass-through data handed to the
  caller, not branch conditions.
- It keeps `Models.cs` and the public surface unchanged (a goal).
- For the auth path specifically, `CompleteOtpLoginResponseDto` marks
  `sessionJwt`/`isNewUser` required; the SDK passes `SessionJwt` through and
  stores it (`PeakClient.cs:97`), then **fails closed on an incomplete stored
  session** at authenticate time (`PeakClient.cs:152`), so a missing
  `sessionJwt` is still caught downstream.

**Consumer-facing caveat (stated contract).** The fields that now default
silently — notably `AccountResponse.Id` and `AccountAddressResponse.Address` —
are returned to the caller **un-validated**. In a wallet, a silently
empty/defaulted address or account id is a fund-loss-shaped failure if the
consumer trusts it (e.g. displays it or sends to it). The SDK itself does not
branch on these fields, but #14's strictness was incidentally guarding this
consumer boundary, so: (1) the plan adds a one-line note to the README / docs
consumer guidance that **consumers MUST null/empty-check identity and address
fields before use**, and (2) a future `[JsonRequired]` hardening pass (the
considered alternative below) is the recommended fix if the team later wants the
SDK itself to fail closed on these. This makes the reliance on consumer
discipline an explicit contract rather than an implicit assumption.

**Considered alternative (not chosen):** add `[JsonRequired]` to the genuinely
required public fields so STJ fails closed like #14. Rejected for this issue
because it alters `Models.cs`/attributes (touching the public surface), makes the
SDK *less* forward-compatible to additive/partial server responses, and is not
required by issue #18. If the team later wants strict response validation, it is
a clean, separate follow-up. This loosening is recorded as **R6** (Section 8).

### 6.4 Packaging + versions

`peak-sdk-csharp.csproj`:

- Remove `<PackageReference Include="Newtonsoft.Json" />` and
  `<PackageReference Include="System.ComponentModel.Annotations" />` (lines 37, 41).
- Remove the generated-client `<ProjectReference … PrivateAssets="all" />`
  (lines 51-54), the `TargetsForTfmSpecificBuildOutput` property, and the
  `_IncludeGeneratedClientDll` target (lines 55-62). The nupkg then ships
  `KyuzanInc.Peak.Sdk.dll` only.

`Directory.Packages.props`:

- `System.Text.Json` **10.0.8 → 8.0.5** (line 24). **Safe downgrade, not a CVE
  fix:** 8.0.5 clears CVE-2024-43485 (fixed 8.0.5) and is past CVE-2024-30105
  (fixed 8.0.4); the current 10.0.8 is also non-vulnerable, and the DTOs use no
  `[JsonExtensionData]`, so neither advisory applies to this code regardless.
  The move is purely Unity-restore alignment with `KyuzanInc.Turnkey.Sdk`
  (which transitively pulls 8.0.5).
- Remove the central `Newtonsoft.Json` and `System.ComponentModel.Annotations`
  `PackageVersion`s (lines 38, 42) and their comment. The generated client keeps
  its own `VersionOverride` for those (it is `IsPackable=false`, build/test-only),
  so `CentralPackageTransitivePinningEnabled=true` restore stays valid.
- Optional consistency cleanup: `Microsoft.Extensions.Http.Polly 7.0.20`
  (lines 29-30) is dead central config — referenced by no project. Removing it in
  the same edit is a cheap win (run a locked-mode restore to confirm); not a
  blocker.

**Refresh all THREE lock files** (`dotnet restore`, committed together — CI
restores `--locked-mode` over the whole solution): the SDK src
(`packages/peak-sdk-csharp/src/packages.lock.json`), the tests
(`packages/peak-sdk-csharp/tests/packages.lock.json`), **and the generated
client** (`packages/peak-public-api-client-csharp/packages.lock.json`) — the last
one has a central `System.Text.Json` entry that the 10.0.8→8.0.5 change
invalidates, and the solution restore covers it.

### 6.5 Files removed

- `packages/peak-sdk-csharp/src/Mapping/GeneratedDtoMappers.cs` (generated→public mappers).
- `packages/peak-sdk-csharp/src/Serialization/TolerantEnumContractResolver.cs` (Newtonsoft-only).
- `packages/peak-sdk-csharp/tests/GeneratedDtoMapperTests.cs` — the mappers are gone.

**Files kept (a correction over an earlier draft):**

- `packages/peak-sdk-csharp/tests/GeneratedClientInternalizationTests.cs` is
  **kept, not deleted.** It asserts `typeof(Gen.*).Assembly.GetExportedTypes()`
  is empty — i.e. the *generated client assembly* exposes no public type (the
  D13 internalize invariant). `PublicSurfaceBaselineTests` does **not** cover
  this: it inspects only `typeof(PeakClient).Assembly` (the SDK assembly), so
  after Option C removes the SDK→client `ProjectReference` its
  `NotContain("PublicApiClient")` becomes trivially true and proves nothing about
  the generated assembly. The two tests guard *different* assemblies. The test
  project keeps its `ProjectReference` to the generated client (Section 6.7, for
  the contract test), so `Gen.*` still resolves and this test compiles and runs
  unchanged at near-zero cost — and it keeps guarding that a future regeneration
  without the internalize step cannot silently re-expose generated types.
- `PeakResponseJson.cs` is **kept** (STJ-only, Section 6.2) per OQ1, or folded
  into the transport; the PLAN picks one.

### 6.6 Request path + signed envelopes unchanged

`PrivateKeyService` still builds `global::Turnkey.Http.SignedRequest` envelopes
and serializes the request DTOs with STJ; `TurnkeyWireFormatSmokeTests` locks
those exact bytes independently of the response path and must stay green. Only
the **response** generics on the three private-key calls change
(`PrivateKeyService.cs:68,132,209`); the stamp construction, headers, and method
signatures are untouched. (Pre-existing, out of scope: the `Debug` request-body
log at `DefaultPeakHttpClient.cs:71` emits the OTP code and signed envelopes — see
R7.)

### 6.7 Preserve drift detection (spec→client kept; client→DTO is a P0 contract test)

- **Spec → generated-client:** unchanged — `openapi-client-drift`
  (`csharp-ci.yml:213-247`) keeps regenerating and diffing the client. Keep the
  generated client project, its codegen, and the internalize step; keep
  `InternalsVisibleTo("KyuzanInc.Peak.Sdk.Tests")` (`AssemblyAttributes.cs:7`) so
  the contract + internalization tests can read the (internal) generated DTO
  shapes. **Remove the now-dead `InternalsVisibleTo("KyuzanInc.Peak.Sdk")`**
  (`AssemblyAttributes.cs:6`): once the SDK drops its generated-client
  `ProjectReference` (Section 6.4) the SDK assembly cannot see those internals at
  all, so the friend grant is dead config — delete that one line, keep the
  `.Tests` one. (`AssemblyAttributes.cs` lives outside the regenerated `src/`
  tree, so the drift job does not touch it.)
- **Generated-client → hand-DTO (P0):** add
  `packages/peak-sdk-csharp/tests/GeneratedDtoContractTests.cs`
  (`[Trait("Category","Contract")]`). For each adopted endpoint, compare the
  **public DTO's JSON field set against the generated DTO's** (public side via
  STJ `JsonPropertyName`/camelCase policy, generated side via its Newtonsoft
  `[DataMember]`/`[JsonProperty]` names), asserting **two** directions:
    1. **Coverage (generated ⊆ public):** every generated-DTO field is present
       on the public DTO — this is the drift signal that matters (a server field
       the public DTO fails to model surfaces here).
    2. **No silent public-only additions, modulo an allowlist:** the public DTO
       introduces no field absent from the generated set **except** an explicit,
       hard-coded allowlist of legacy public-only fields — currently exactly
       `{ UserResponse.IsAuthenticated }` (no server-spec source; the mapper
       leaves it default — `GeneratedDtoMappers.cs:42-50`). A new public-only
       field must be added to the allowlist deliberately, which is the intended
       review friction.
  For fields present on **both** sides, assert the **type category**
  (string / number / bool / object / array) matches. The test deliberately does
  **not** assert required-ness (Section 6.3 relaxes that on purpose) and does
  **not** require exact set equality (which `IsAuthenticated` would break — the
  earlier "set equals" framing was the r3 Codex BLOCKER). The test project keeps
  its existing `ProjectReference` to the generated client
  (`tests/peak-sdk-csharp.Tests.csproj:15`) — it is **used** by this test (and by
  the retained `GeneratedClientInternalizationTests`, Section 6.5), which
  resolves the otherwise-dangling reference (Section 6.5 deletes only
  `GeneratedDtoMapperTests`). `Category=Contract` satisfies the existing
  `--filter "Category!=E2E"` (`csharp-ci.yml:89`), so it runs in the standard
  unit job with no workflow change; this introduces the first
  `[Trait("Category",...)]` in the suite. (Note: no test is currently tagged
  `Category=E2E`, so the existing E2E CI step at `csharp-ci.yml:91-99` is
  presently a no-op; adding the first `Contract` trait does not change that.
  Tagging or pruning the dead E2E partition is a separate, out-of-scope concern.)

### 6.8 AOT / IL2CPP safety guarantees

- Every (de)serialized type is registered in `PeakJsonContext`
  (`[JsonSerializable]`), including the new `UpdateAccountDisplayNameEnvelope`.
  A missing registration throws at runtime — the actual Unity failure mode — so
  Section 6.9 exercises a real STJ resolution in CI to catch it.
- No reflection-based STJ: the transport resolves a `JsonTypeInfo<T>` from the
  source-gen context and throws if absent (Section 6.2). No
  `UnmappedMemberHandling`/`ReferenceHandler`/custom converters (Section 1.3).
- No RestSharp / Newtonsoft anywhere on the consumer closure (Section 6.4,
  asserted by Section 6.9).

### 6.9 Unity-shaped consumer assertion (CI)

This repo has no Unity adapter, so the realistic, license-free proxy is a
**dependency-graph assertion (both TFMs) + an executed STJ-resolution smoke
(net8.0 only)**. Two layers with different reach, because **`netstandard2.1` is a
library target that cannot run** — the existing `consumer-restore-check`
(`csharp-ci.yml:108`) builds the consumer over the `[netstandard2.1, net8.0]`
matrix but never executes it (`dotnet build`, no `dotnet run`; the project has no
`OutputType`). So:

- **Static layer — both TFMs (extends `consumer-restore-check`):** the
  banned-package graph check + the packed-nuspec `<dependency>` assertion below.
  These need only restore + build, so they run on `netstandard2.1` (the TFM Unity
  restores) **and** `net8.0`.
- **Execution layer — net8.0 only (a separate runnable console step/job):** the
  STJ-resolution smoke must actually *run* to surface a missing
  `[JsonSerializable]` registration, so it is a distinct `net8.0` console project
  with `<OutputType>Exe</OutputType>` driven by `dotnet run`. It does **not** run
  on `netstandard2.1` (un-runnable). This proves the STJ source-gen path resolves
  on .NET 8; the IL2CPP/Mono *runtime* execution proxy is still deferred to W5
  (OQ3) — the in-repo net8.0 run is the strongest license-free signal available
  here, not a substitute for the Unity runtime smoke.

Concretely:

1. **Banned-package check — `RestSharp`, `Polly`, `Newtonsoft.Json` only**
   (do **not** ban `System.ComponentModel.Annotations`: it is a legitimate
   transitive of `System.Text.Json 8.0.5` on netstandard2.1 and banning it would
   false-positive on a correct STJ-only build):

   ```bash
   for banned in RestSharp Polly Newtonsoft.Json; do
     if dotnet list consumer.csproj package --include-transitive | grep -qi "$banned"; then
       echo "::error::$banned leaked into the consumer graph (Unity/IL2CPP regression)"; exit 1
     fi
   done
   ```

   Additionally assert on the **packed nuspec `<dependency>` list** (the layer
   that actually drives Unity collisions): it must list neither `Newtonsoft.Json`
   nor `System.ComponentModel.Annotations`, and must pin `System.Text.Json`
   8.0.x.
2. **STJ-resolution smoke (public path only, net8.0 runnable console):** a
   **separate** `net8.0` console project with `<OutputType>Exe</OutputType>`,
   driven by `dotnet run` (not the build-only `consumer-restore-check` project),
   exercises the real STJ response path **through public API** — `PeakJsonContext`
   is `internal` (`PeakJsonContext.cs:55`) and `PeakResponseJson` is `internal`,
   so the fresh external consumer **cannot** touch them directly. Instead,
   construct a public `DefaultPeakHttpClient` over a stub `HttpMessageHandler`
   that returns a canned `200` body (e.g. `{"otpId":"x"}`) and call the public
   `GetAsync<InitOtpLoginResponse>(...)` (drive it synchronously with
   `.GetAwaiter().GetResult()`; net8.0 `Main` could also be `async Task`). That
   public call routes through
   `PeakResponseJson.Deserialize<InitOtpLoginResponse>` →
   `PeakJsonContext.Default.GetTypeInfo(...)`, so a missing `[JsonSerializable]`
   registration (the real Unity runtime failure) throws a non-zero exit and fails
   CI rather than only at runtime on a device. (`InitOtpLoginResponse` is
   registered today — `PeakJsonContext.cs:30`.) The PLAN decides placement: a new
   `dotnet run` step in `consumer-restore-check` gated to
   `matrix.tfm == 'net8.0'`, or a small dedicated job; either keeps
   `netstandard2.1` build-only.

Fix the **stale comments in `consumer-smoke.yml` only** (`:11-13` "a missing
embedded DLL, a wrong/absent dependency edge (Newtonsoft.Json,
System.ComponentModel.Annotations)" and `:121-123` "everything else
(Newtonsoft.Json, System.ComponentModel.Annotations, …) from nuget.org"): there
is no longer an embedded DLL and those edges are gone. `csharp-ci.yml`'s
`consumer-restore-check` has **no** Newtonsoft comment to fix — only the new
assertion is added there.

### 6.10 Public surface + baseline

`PublicSurfaceBaselineTests` is an **invariant** test, **not** a committed
snapshot — there is **no `PublicApi.Sdk.approved.txt`** in the repo (the test's
own comment says a pinned snapshot is too fragile). It calls
`GeneratePublicApi()` and asserts `NotContain("PublicApiClient")` plus presence
of `PeakClient` / `Models` / `AccountResponse` / `IPeakHttpClient`. Therefore:

- Removing the `IPeakHttpClient` Newtonsoft XML remark **cannot** shift the
  baseline (XML doc comments do not appear in `GeneratePublicApi()` output);
  there is nothing to "update".
- Once the generated-client `ProjectReference` is removed from the SDK, the SDK
  assembly no longer references the `KyuzanInc.Peak.PublicApiClient` namespace at
  all, so `NotContain("PublicApiClient")` stays trivially true.
- **Add a negative assertion** that the new wrapper is not public, e.g.
  `api.Should().NotContain("UpdateAccountDisplayNameEnvelope")`, because the
  invariant test would otherwise not catch an accidental `public` on it.

### 6.11 Docs (enumerated edits)

The repo enforces HIGH-severity doc-drift discipline (commit `d30b742`), so name
the exact spots:

- `docs/architecture.md:37` — dependency-stack diagram says
  `System.Text.Json 10.0.8` → `8.0.5`.
- `docs/architecture.md:81` (exact: "the hand-designed DTOs wrap the generated
  types") and the dependency-stack boxes at `:20` and `:26-29` (which frame
  `KyuzanInc.Peak.PublicApiClient` as a runtime-referenced layer,
  `IsPrivateAssets="all"`) — rewrite so the DTOs are hand-written STJ (not
  wrappers) and the generated client is build-time + drift-only (not a runtime
  dependency edge).
- `docs/sync-rules.md:96-102` ("Consumer wiring") — currently "the transport
  deserializes them with Newtonsoft and `Mapping/GeneratedDtoMappers.cs` maps
  them"; rewrite to STJ-only, no Newtonsoft, mappers deleted.
- `README.md:22` — "Re-exposed by the SDK behind hand-designed DTOs" (the
  `KyuzanInc.Peak.PublicApiClient` row of the Packages table) becomes inaccurate
  under Option C; correct it (client is build-time/drift-only).
- `plans/plans-peak-sdk-csharp.md` — W0c (`:50`, "Consumed by the SDK as of
  PR #14 … mapped to the public surface") update to "build-time + drift-only;
  runtime response path reverted to STJ per #18"; **retire W0d (`:51`, Unity
  link.xml for Newtonsoft — moot, Newtonsoft is off the runtime path) and W0e
  (`:52`, slim the embedded client to models-only — moot, not embedded)**;
  optionally cross-link this spec from the W5 row. Note `:173` already says
  `System.Text.Json 8.0.5`, so the plans file is internally inconsistent with
  the current 10.0.8 pin even today — this change makes it consistent.

---

## 7. Testing strategy

- **Keep green unchanged:** `TurnkeyWireFormatSmokeTests` (requests/envelopes),
  `PeakClientTests`, `PeakErrorTests`, `SessionJwtTests`, `InMemoryStorageTests`,
  `PublicSurfaceBaselineTests` (plus the new negative assertion, Section 6.10),
  and `GeneratedClientInternalizationTests` (retained per Section 6.5 — guards
  the generated assembly's exported-types invariant).
- **Rework service tests** (`AuthServiceTests`, `AccountServiceTests`,
  `PrivateKeyServiceTests`): mocks return the **public** DTOs instead of
  `Gen.*ResponseDto`. Happy-path assertions are unchanged. The R6-style "custom
  transport" test now deserializes a public DTO with STJ (no Newtonsoft).
  **`PrivateKeyServiceTests.Export_UnsupportedSourceType_ThrowsInvalidResponse`:**
  reword — the unknown `sourceType` now passes through as a **raw string** (not
  nulled by the removed resolver) and reaches the `default:` arm of the
  source-type switch, still throwing `InvalidResponse`. Update the in-code
  comment that references the tolerant resolver.
- **Rework transport / response-json tests** (`DefaultPeakHttpClientTests`,
  `PeakResponseJsonTests`) to drive a public DTO through the STJ path. Enumerate
  the exact assertion changes (these are **not** "unchanged"):
  - unknown enum string (e.g. `chainType:"aptos"`) → **passthrough raw string**
    (was: `null` via the resolver). Frame as a forward-compat improvement.
  - non-string enum token (e.g. `chainType:1`) → **`InvalidResponse`** (was:
    softened to `null`).
  - non-integral `accountIndex` (e.g. `1.5`) → still `InvalidResponse` (STJ
    rejects into `int`). Preserved.
  - integral-but-decimal-token `accountIndex` (e.g. `1.0`) → now
    **`InvalidResponse`** (STJ rejects a `.`-bearing token into `int`). This is a
    **behavior change** vs the #14 generated path, which accepted `1.0` as `1`
    (decimal deserialize + truncation check, `GeneratedDtoMappers.cs:30`). It
    **restores the pre-#14 STJ-into-`int` contract** and is an accepted narrowing
    (Section 6.3 / R8). Add a test asserting `1.0 → InvalidResponse`.
  - missing required field → now **silently defaults** (Section 6.3 / R6); add a
    test documenting this new behavior rather than asserting a throw.
  - malformed body → `InvalidResponse`; empty body → `null`. Preserved.
- **New P0 `GeneratedDtoContractTests`** (`Category=Contract`, Section 6.7):
  generated-⊆-public field coverage + type-category match for shared fields +
  an explicit public-only allowlist (`{ UserResponse.IsAuthenticated }`). **Not**
  exact set equality (that would false-fail on `IsAuthenticated`).
- **CI** (Section 6.9): banned-package check (RestSharp/Polly/Newtonsoft),
  nuspec `<dependency>` assertion, STJ-resolution smoke; locked-mode restore over
  all three lock files.
- **Packaging check:** `dotnet pack`; assert the nupkg contains
  `KyuzanInc.Peak.Sdk.dll` and **not** `KyuzanInc.Peak.PublicApiClient.dll`.

## 8. Risks

- **R1 — STJ 8.0.5 downgrade breaks a transitive constraint.**
  `Microsoft.Extensions.Http 8.0.1` and the net8.0 shared framework are
  8.0-wave; 8.0.5 aligns rather than conflicts. Mitigation: locked-mode restore
  on all three TFMs; consumer-restore-check builds netstandard2.1 + net8.0.
- **R2 — a response shape the public DTO does not model (field presence).**
  Covered by the reworked service/transport tests and the P0 contract test
  (generated-⊆-public field coverage + type-category match, Section 6.7).
  Distinct from R6 (runtime required-ness).
- **R3 — removing the embedded DLL breaks `dotnet pack`/restore expectations.**
  Packaging check + consumer-restore-check on both TFMs.
- **R4 — public-surface or wire drift.** `PublicSurfaceBaselineTests` (+ the
  wrapper negative assertion) and `TurnkeyWireFormatSmokeTests`.
- **R5 — orphaned usings / format.** Deleting files/usings can orphan a `using`
  (esp. `System.Reflection` in `PeakResponseJson.cs`, Section 6.2). Mitigation:
  build `-warnaserror` + `dotnet format --verify-no-changes`.
- **R6 — required-field validation is relaxed (headline behavior change).**
  Generated DTOs were `IsRequired=true` (Newtonsoft fail-closed); public STJ DTOs
  are nullable, so a missing required field now deserializes to default instead
  of `InvalidResponse`. **Accepted** (Section 6.3): restores pre-#14 behavior;
  the SDK still fails closed on control-flow fields. **Consumer caveat:**
  identity/address fields (`AccountResponse.Id`, `AccountAddressResponse.Address`)
  return un-validated, so consumers must null/empty-check them; a `[JsonRequired]`
  hardening pass is the recommended follow-up for SDK-side fail-closed.
  Documented in the README consumer guidance (a "Response validation" note) and
  R6 here so it is a signed-off decision, not a surprise.
- **R7 — pre-existing Debug request-body logging.** `DefaultPeakHttpClient.cs:71`
  logs the OTP code and signed envelopes at `Debug`. Not introduced or changed
  here; recommend a follow-up to redact `body=` for the auth/private-key
  endpoints (or gate behind explicit opt-in). The transport threat surface is
  otherwise unchanged.
- **R8 — `accountIndex` numeric-token narrowing (secondary behavior change).**
  `accountIndex` is OpenAPI `type: number` (`public-api.yaml:1448`), generated as
  `decimal` and the public DTO is `int` (`Models.cs:16`). The #14 generated path
  accepted a decimal-token `1.0` as `1` (truncation check,
  `GeneratedDtoMappers.cs:30`); deserializing the response **directly** into the
  `int` field with STJ rejects any `.`-bearing token (`1.0`, `1.5`) as
  `InvalidResponse`. **Accepted:** it restores the exact pre-#14 STJ-into-`int`
  behavior, and `accountIndex` is an integer index the server emits as an integer
  token (`1`, not `1.0`) in practice; `1` still deserializes fine. **Considered
  alternative (not chosen):** add a tolerant `JsonConverter<int>` (or an internal
  `decimal`-typed wire field + map) to keep `1.0` accepted — rejected because it
  reintroduces a custom converter, eroding the "no custom converters" AOT-safety
  property (Section 1.3) for a token shape the server does not emit. If the team
  later sees real `1.0` payloads, the tolerant converter is the clean follow-up.
  Enumerated as a §7 test (`1.0 → InvalidResponse`).

## 9. Implementation outline

1. Retype the three services to public response DTOs (retype
   `GetAddressDetailAsync` and `ExportPrivateKeyAsync` together); add the
   internal `UpdateAccountDisplayNameEnvelope` + its `PeakJsonContext`
   registration; delete `GeneratedDtoMappers.cs` and the `Gen`/`Mapping` usings.
2. Simplify `PeakResponseJson` to STJ-only (drop `System.Reflection` + Newtonsoft
   usings) or inline into the transport; delete `TolerantEnumContractResolver.cs`;
   drop the Newtonsoft catch arm and the `IPeakHttpClient` Newtonsoft remark;
   keep the empty-body short-circuit.
3. csproj: remove Newtonsoft/Annotations refs and the embedded-client
   ProjectReference + target. Remove the now-dead
   `InternalsVisibleTo("KyuzanInc.Peak.Sdk")` from the generated client's
   `AssemblyAttributes.cs` (keep the `.Tests` one — Section 6.7).
   `Directory.Packages.props`: STJ → 8.0.5, remove Newtonsoft/Annotations central
   versions (optional: drop dead Http.Polly). Refresh **all three** lock files.
4. Rework the tests (services, transport, response-json) per Section 7; delete
   **only** the generated-DTO mapper test (`GeneratedDtoMapperTests`); **keep**
   `GeneratedClientInternalizationTests` (Section 6.5); add the P0 contract test
   (it and the retained internalization test both keep the test ProjectReference
   live); add the wrapper negative assertion to `PublicSurfaceBaselineTests`.
5. CI: add the banned-package graph check + packed-nuspec `<dependency>`
   assertion to `consumer-restore-check` (both TFMs); add the **net8.0-only**
   runnable STJ-resolution smoke (stub `HttpMessageHandler` →
   `GetAsync<InitOtpLoginResponse>`, `OutputType=Exe`, `dotnet run`) as a step
   gated to `matrix.tfm == 'net8.0'` or a small dedicated job (Section 6.9); fix
   the stale comments in `consumer-smoke.yml`.
6. Docs: the enumerated edits in Section 6.11.
7. Verify locally where possible (GitHub Packages auth) and rely on CI for the
   full solution, the drift job, and the consumer smokes.

## 10. Open questions (resolved by recommendation; flag if you disagree)

- **OQ1 — keep `PeakResponseJson` or inline it?** Recommendation: keep it as a
  thin STJ-only helper (one call site, minimal churn, easy to test); drop its
  now-orphaned `using System.Reflection;`.
- **OQ2 — contract-test priority.** **Resolved: P0** (Section 3, 6.7). It is the
  only guard for hand-DTO drift once #14's compile binding is gone, and the test
  project already references the generated client, so cost is low.
- **OQ3 — full Unity IL2CPP runtime smoke.** Recommendation: **follow-up in
  `peak-sdk-unity` (W5)** (needs a licensed Unity runner). The in-repo proxy
  (Section 6.9) proves the dependency closure + STJ-only (de)serialization but
  **not** IL2CPP AOT execution; issue #18 AC1 is only fully closed once the W5
  runtime smoke passes. Track it as a new follow-up row in
  `plans-peak-sdk-csharp.md` and link it here.
- **OQ4 — keep the generated client + codegen + drift job?** Recommendation:
  **yes, keep all three** (build-time only, internalized, `InternalsVisibleTo`
  the test assembly for the contract test).
- **OQ5 — required-field strictness.** **Resolved: accept the loosening**
  (Section 6.3, R6); `[JsonRequired]` hardening is a possible separate follow-up.

## 11. Acceptance-criteria mapping (issue #18)

| Issue #18 acceptance criterion | Status | Where |
| --- | --- | --- |
| NuGetForUnity / IL2CPP restore + RUN OTP→Authenticate→Accounts→Import/Export with no `TypeLoadException`/`FileNotFoundException`, no missing RestSharp/Polly | **Partially satisfied** (graph + STJ-resolution proxy; IL2CPP AOT *execution* deferred to W5) | §6.1–6.4 (STJ-only path, no embedded client); §6.9 (CI graph + nuspec + STJ-resolve); OQ3 (W5 runtime smoke) |
| No Newtonsoft.Json on the consumer (or non-colliding) | Satisfied | §6.4 (dependency + central version removed); §6.9 asserts absence |
| `System.Text.Json` pinned 8.0.x; all (de)serialization via source-gen, no reflection fallback | Satisfied | §6.4 (8.0.5); §6.2, §6.8 |
| No RestSharp on the consumer runtime path (removed or provably never loaded) | Satisfied | §6.4 (embedded DLL + ProjectReference removed); §6.9; §7 packaging check |
| Existing `dotnet test` green; public API surface unchanged | Satisfied (with documented test-assertion updates, §7) | §6.10 (`PublicSurfaceBaselineTests`), §6.6 (`TurnkeyWireFormatSmokeTests`), §7 |

## 12. References

- Issue: [KyuzanInc/peak-sdk-csharp#18](https://github.com/KyuzanInc/peak-sdk-csharp/issues/18); downstream [KyuzanInc/peak#330](https://github.com/KyuzanInc/peak/pull/330) (W5).
- Prior design (superseded for the response layer):
  `docs/superpowers/specs/2026-06-01-consume-generated-openapi-client-design.md`; its plan `docs/superpowers/plans/2026-06-01-consume-generated-openapi-client.md`.
- Source: `packages/peak-sdk-csharp/src/PeakJsonContext.cs`,
  `Models/Models.cs`, `Serialization/PeakResponseJson.cs`,
  `Serialization/TolerantEnumContractResolver.cs`,
  `Utils/DefaultPeakHttpClient.cs` (issue #18 cites a stale `Http/` path),
  `Utils/IPeakHttpClient.cs`, `Mapping/GeneratedDtoMappers.cs`,
  `Services/{AuthService,AccountService,PrivateKeyService}.cs`,
  `tests/{PublicSurfaceBaselineTests,PrivateKeyServiceTests}.cs`.
- Build/CI: `peak-sdk-csharp.csproj`, `Directory.Packages.props`,
  `tests/peak-sdk-csharp.Tests.csproj`, `.github/workflows/csharp-ci.yml`,
  `.github/workflows/consumer-smoke.yml`.
- Docs touched: `docs/architecture.md`, `docs/sync-rules.md`, `README.md`,
  `plans/plans-peak-sdk-csharp.md`.
- `CLAUDE.md` (generated-code / crypto / logging / locked-mode constraints).

## 13. Change log

- **2026-06-03 r4** — round-3 review (independent Codex re-review, gpt-5.5
  xhigh) found three test/CI-design issues; all fixed, Option C unchanged:
  (BLOCKER, §6.7) the P0 contract test said the public DTO field set "equals"
  the generated set, but `UserResponse.IsAuthenticated` is a legacy public-only
  field with no generated counterpart (`GeneratedDtoMappers.cs:42-50`), so exact
  equality would false-fail — re-specified as generated-⊆-public coverage + a
  hard-coded public-only allowlist `{ UserResponse.IsAuthenticated }` + shared-
  field type-category match (also updated §3 and §7); (MAJOR, §6.5) reversed the
  `GeneratedClientInternalizationTests` deletion — `PublicSurfaceBaselineTests`
  inspects only the SDK assembly, so it cannot prove the *generated* assembly
  stays internalized; the test is kept (the test ProjectReference stays live for
  it and the contract test) (also updated §7 keep-green list and §9 step 4);
  (MINOR, §6.9) the external consumer STJ smoke can't enumerate the `internal`
  `PeakJsonContext`, so re-specified it as a public-path smoke (stub
  `HttpMessageHandler` → `DefaultPeakHttpClient.GetAsync<InitOtpLoginResponse>`).
  The subsequent independent Codex re-review (gpt-5.5 xhigh) returned **READY,
  no blocking issue**, and surfaced three non-blocking refinements, also folded:
  (MAJOR, §6.9) split the CI proxy into a both-TFM static graph/build check and a
  **net8.0-only runnable** STJ-execution smoke (`netstandard2.1` cannot run);
  (MAJOR, §6.3/§7/R8) called out the `accountIndex` decimal-token narrowing
  (`1.0` was accepted by #14, now `InvalidResponse` under STJ-into-`int` — an
  accepted pre-#14 restoration, with the tolerant-converter alternative recorded);
  (MINOR, §6.7/§9) added an explicit step to delete the dead
  `InternalsVisibleTo("KyuzanInc.Peak.Sdk")` friend grant.
- **2026-06-03 r2** — revised after Codex + 4-lens (engineering/security/
  completeness/skeptic) review: corrected the §6.9 CI banned-list (dropped
  `System.ComponentModel.Annotations`, a benign STJ transitive); fixed the
  §6.10 public-surface claim (invariant test, no `approved.txt`); added §6.3
  required-field-validation decision + R6; promoted the contract test to P0
  (§6.7); enumerated the §6.4 third lock file and §6.11 exact doc edits; framed
  STJ 8.0.5 as a safe downgrade with CVE notes; enumerated the §7 test-assertion
  changes; renamed the wrapper to `UpdateAccountDisplayNameEnvelope`; added the
  R7 Debug-logging note. Core decision (Option C) unchanged and validated against
  this repo's .NET build/test/pack closure during review (IL2CPP runtime
  execution deferred to W5).
- **2026-06-03 r3** — round-2 review (Codex "ready, no blocking issue" + 2-lens
  "ready-with-fixes", no remaining design blocker): added the §6.3/R6
  consumer-discipline caveat (`Id`/`Address` default silently → consumers must
  null/empty-check) and the auth-field (`sessionJwt`/`isNewUser`) note; trimmed
  the "end-to-end" overclaim to the .NET build/test/pack path; corrected §1.3
  (`chainType` is a caller arg, not a response field), §6.5 (test deletion is a
  redundancy cleanup), §6.11 (`README.md:22`, tightened architecture citations);
  noted the currently-vacuous `Category=E2E` partition in §6.7.
- **2026-06-03 r1** — initial draft.
