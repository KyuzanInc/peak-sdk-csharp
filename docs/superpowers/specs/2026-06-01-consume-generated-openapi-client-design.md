# Design — Consume the generated OpenAPI client in the public SDK

- **Date:** 2026-06-01
- **Status:** Draft (revised after an empirical codegen spike; see Section 3)
- **Topic:** Wire the internal generated client `KyuzanInc.Peak.PublicApiClient`
  into the public SDK `KyuzanInc.Peak.Sdk`.
- **Tracks:** Follow-up 1 ("Consumer wiring") in
  `docs/superpowers/plans/2026-05-29-csharp-public-api-client-codegen.md`;
  W0c in `plans/plans-peak-sdk-csharp.md`.

---

## 1. Context

`KyuzanInc.Peak.PublicApiClient` is an internal, auto-generated C# client
produced by `scripts/generate-public-api-client.sh` from the pinned spec
`upstream-snapshots/peak-server-openapi/public-api.yaml`, using the
openapi-generator `csharp` / `restsharp` library at core 7.9.0 (pinned by
`openapitools.json`, decision D24). Its DTOs are **Newtonsoft.Json**-based; its
API classes use **RestSharp**. It is `IsPackable=false` and referenced by
nothing. An `openapi-client-drift` CI job regenerates it and fails on any diff.

The public SDK `KyuzanInc.Peak.Sdk` hand-rolls its HTTP layer (`PeakClient` +
services + the `IPeakHttpClient` abstraction) and uses **System.Text.Json with
source generation** (`PeakJsonContext`) for its DTOs. The SDK covers a subset of
the spec: `AuthService` (OTP init/complete), `AccountService` (list,
list-addresses, update-display-name, internal get-address-detail), and
`PrivateKeyService` (init-import, complete-import, export).

This design makes the SDK consume the generated client's **response** DTOs so
those shapes are driven by the pinned spec instead of being hand-maintained,
**without** leaking generated types onto the SDK public surface.

### 1.1 Facts confirmed during exploration

- The SDK runtime graph has **zero Newtonsoft.Json** today
  (`packages/peak-sdk-csharp/src/packages.lock.json`).
- There is **no PublicApiGenerator baseline test yet**; the public surface is
  not machine-locked. The task brief assumed one exists.
- The generated private-key request DTOs reference the generated
  `KyuzanInc.Peak.PublicApiClient.Model.SignedRequest`, while the SDK builds
  `Turnkey.Http.SignedRequest`. `TurnkeyWireFormatSmokeTests` already locks the
  exact wire bytes of those signed envelopes.

## 2. Goals and non-goals

### Goals

- The SDK's **response** wire shapes come from the generated client, so spec
  drift surfaces at compile time instead of by hand-reading.
- The SDK public surface stays System.Text.Json + source generation (D6) and
  gains **no** generated types (D13).
- The generated client stays internal: referenced via `ProjectReference` with
  private-assets hardening so SDK consumers cannot depend on it.
- All existing tests stay green; byte-for-byte wire compatibility is preserved.

### Non-goals

- No new endpoints. The SDK surface stays the current Auth/Account/PrivateKey
  set. The other generated API groups are not wired.
- No change to the public DTO shapes in `Models/Models.cs`.
- No change to the request path (Section 6 explains why requests stay
  hand-written for now).
- No change to the generator **config** (library, JSON stack) or the spec source
  (D23/D24 honored): the generated DTO *shapes* are consumed as they exist today.
  The only change to the generated client is a visibility flip to `internal`
  (Section 6.1), applied by a deterministic step the generation script gains;
  that updates the committed client and the drift baseline once.

## 3. Empirical findings that shaped this design

An earlier draft proposed regenerating the client as "System.Text.Json,
models-only" (the brief's Option 3). A codegen spike against the pinned 7.9.0
core showed this is **not achievable**:

- **`httpclient` + `useSystemTextJson=true` still emits Newtonsoft in 7.9.0.**
  The generated project references `Newtonsoft.Json 13.0.3` and `JsonSubTypes`
  and contains no `System.Text.Json`; the models carry `[DataContract]` /
  `[DataMember]` and `[JsonConverter(typeof(StringEnumConverter))]`. The
  `useSystemTextJson` flag is ignored by this library at this core, exactly as
  it is for `restsharp`.
- **`generichost` emits System.Text.Json but is not standalone.** Optional
  properties are wrapped in `Option<T>` (defined in `Client/Option.cs`), and the
  per-enum converters are registered through `Client/HostConfiguration`, not via
  attributes on the models. A source-generation context over those models alone
  would drop the converters, so `chainType: "evm"` would fail or emit a number.
- **Every library aliases model types to `Client/` support types**
  (`OpenAPIDateConverter`, `FileParameter`, …), so a `models`-only generation
  that omits `Client/` does not compile.

Conclusion: the pinned 7.9.0 core (D24) cannot produce clean,
source-gen-registerable, dependency-light STJ models. Aligning the generated
client onto STJ would need a generator-core bump, which is out of scope here.
We therefore consume the client **as it is generated today** — Newtonsoft
models — and convert at the SDK boundary. This matches the wording of Follow-up
1, which already names "map generated Newtonsoft DTOs → the public System.Text.Json
surface" as the expected path.

## 4. Decision

**Adopt Option 1: deserialize the generated Newtonsoft response DTOs with
Newtonsoft inside the SDK, map them to the public System.Text.Json DTOs, and
keep the SDK's own `IPeakHttpClient` transport. The generated client is consumed
as committed — no regeneration.**

### 4.1 Why not the alternatives

- **Option 3 — regenerate to STJ models.** Empirically impossible to do cleanly
  at the pinned core (Section 3). Rejected.
- **Option 2 — consume the generated RestSharp API classes wholesale.** Bypasses
  `IPeakHttpClient` and its Unity-compatible guarantees (exact
  `application/json`, `X-API-KEY`, `PeakError` mapping) and breaks the smoke
  tests. Rejected.
- **Option 0 — keep hand-written DTOs, add a generated-vs-handwritten contract
  test.** Lowest risk and no new runtime dependency, but it does not remove
  hand-maintained DTOs. Kept as the documented fallback (Section 11, OQ1) if the
  team decides the Newtonsoft cost below is not worth paying yet.

### 4.2 Accepted cost

Option 1 brings **Newtonsoft.Json back into the SDK runtime closure** for
internal response parsing only. This is a partial step back from D6's
"System.Text.Json everywhere for IL2CPP/AOT safety" rationale. It is scoped and
mitigated:

- The **public** serialization path (requests, results, storage) stays STJ
  source-gen. Only internal response **deserialization** uses Newtonsoft.
- The Unity adapter (`peak-sdk-csharp-unity`), which is the IL2CPP consumer,
  must ship a `link.xml` entry so Newtonsoft types survive managed-code
  stripping. This is recorded as R1 and OQ2.
- The committed RestSharp client also references RestSharp and Polly. The SDK
  uses only the `Model` types, so those assemblies are never loaded at runtime
  (the CLR resolves referenced assemblies lazily, on first use of a type from
  them). Slimming the client to models-only is a tracked follow-up (OQ3),
  deferred because it needs a generator-core bump or selective support-file
  curation that 7.9.0 makes fragile (Section 3).

## 5. Architecture

```
peak-server spec (pinned yaml)  ──►  KyuzanInc.Peak.PublicApiClient (committed, Newtonsoft, unchanged)
                                            │  ProjectReference (PrivateAssets=all) + DLL embedded in SDK nupkg
                                            ▼
                                     KyuzanInc.Peak.Sdk
  request: hand DTO ──STJ (PeakJsonContext)──►┐
                                              │ IPeakHttpClient / DefaultPeakHttpClient
  response body string ◄────────────────────┘ (exact application/json, X-API-KEY, PeakError)
        │ Newtonsoft (generated Model types only)
        ▼
  generated response DTO ──Mapping/ (ToPublic)──► public STJ DTO (Models.cs)  ──► caller
```

Data flow for one call (init OTP login):

1. The service builds the existing hand request DTO and serializes it via STJ
   (unchanged), then calls `IPeakHttpClient.PostAsync<InitOtpLoginRequest,
   InitOtpLoginResponseDto>(path, payload)`.
2. The transport sends the request and, because the response type
   `InitOtpLoginResponseDto` lives in the generated client assembly,
   deserializes the body with Newtonsoft.
3. The service maps `InitOtpLoginResponseDto` to the public
   `InitOtpLoginResponse`. No generated type escapes the SDK.

## 6. Detailed design

### 6.1 Reference the client, make it internal, embed it privately (D13)

The generator **config** (library, JSON stack) and the spec source are unchanged,
so the DTO shapes are consumed as generated. Two things make the client genuinely
internal — both are required, because either alone leaks:

1. **The generated types are `internal`.** `BuildOutputInPackage` (below) puts the
   client DLL under `lib/{tfm}`, and NuGet treats every `lib` assembly as a
   compile/reference asset — so `PrivateAssets=all` alone does **not** stop a
   consumer from referencing generated *public* types. Making the generated types
   `internal` removes the public surface entirely, so there is nothing to
   reference at compile time. The brief calls for exactly this ("keep generated
   types internal to the SDK … add `InternalsVisibleTo` only if needed").
2. **`PrivateAssets=all` + embedding** keeps the client off the consumer's package
   dependency graph while still shipping its DLL for runtime.

Internalization is applied by the generation script, not by hand, so drift CI
stays stable. Appended to `scripts/generate-public-api-client.sh` after codegen:

```bash
# Make every generated top-level type internal so the embedded client DLL
# exposes no public API to SDK consumers (D13). Deterministic + portable
# (perl, not GNU/BSD-specific `sed -i`), so the drift job reproduces it.
find "$PKG/src" -name '*.cs' -print0 \
  | xargs -0 perl -i -pe 's/^(\s*)public( (?:partial |sealed |abstract |static )*(?:class|interface|enum|struct|delegate|record)\b)/$1internal$2/'
```

The keyword set (`class|interface|enum|struct|delegate|record`) covers every
top-level kind the generator emits: the committed client has 75 classes, 26
interfaces, 9 enums, and one `public delegate` (`Client/ExceptionFactory.cs`);
`struct`/`record` are included defensively. Missing a kind (the delegate is the
trap) would leave a leaked public type and break accessibility consistency, which
is why the keyword set is explicit and the exported-types-empty test (Section 6.5)
guards it. `public` members of the now-internal types stay as written (their
effective visibility is already bounded by the internal type), and because all
types flip together the client's own API/Client classes stay
accessibility-consistent.

`InternalsVisibleTo` must be emitted **explicitly**. The client csproj sets
`<GenerateAssemblyInfo>false</GenerateAssemblyInfo>`, so MSBuild
`<InternalsVisibleTo>` items would be silently ignored and the SDK would fail to
see the internal DTOs. Add a hand-authored attribute file *outside* the
regenerated `src/` tree (so the script's clean step and drift CI never touch it),
`packages/peak-public-api-client-csharp/AssemblyAttributes.cs`:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("KyuzanInc.Peak.Sdk")]
[assembly: InternalsVisibleTo("KyuzanInc.Peak.Sdk.Tests")]
```

and add it to the client csproj (which has `EnableDefaultCompileItems=false`):

```diff
   <ItemGroup>
     <Compile Include="src/KyuzanInc.Peak.PublicApiClient/**/*.cs" />
+    <Compile Include="AssemblyAttributes.cs" />
   </ItemGroup>
```

`packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj`:

```diff
   <ItemGroup>
     <PackageReference Include="KyuzanInc.Turnkey.Sdk" />
     <PackageReference Include="System.Text.Json" />
     <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
     <PackageReference Include="Microsoft.Extensions.Http" />
+    <!-- Internal response DTOs are Newtonsoft-shaped (Section 3). Declared as a
+         normal dependency so it is present for consumers, because the generated
+         client is embedded privately (below) and cannot carry it transitively. -->
+    <PackageReference Include="Newtonsoft.Json" />
+    <!-- The embedded generated DTOs implement IValidatableObject; netstandard2.1
+         consumers need this assembly to load them (the client's own reference is
+         hidden by PrivateAssets=all). -->
+    <PackageReference Include="System.ComponentModel.Annotations" />
   </ItemGroup>

+  <!--
+    Internal generated DTO client (D13). PrivateAssets=all stops the reference
+    from flowing to SDK consumers as a package dependency; the DLL is embedded
+    into the nupkg by the target below so it loads at runtime (a ProjectReference
+    to an IsPackable=false project is otherwise omitted from the package).
+  -->
+  <ItemGroup>
+    <ProjectReference Include="..\..\peak-public-api-client-csharp\KyuzanInc.Peak.PublicApiClient.csproj"
+                      PrivateAssets="all" />
+  </ItemGroup>
+  <PropertyGroup>
+    <TargetsForTfmSpecificBuildOutput>$(TargetsForTfmSpecificBuildOutput);_IncludeGeneratedClientDll</TargetsForTfmSpecificBuildOutput>
+  </PropertyGroup>
+  <Target Name="_IncludeGeneratedClientDll">
+    <ItemGroup>
+      <BuildOutputInPackage Include="$(OutputPath)KyuzanInc.Peak.PublicApiClient.dll" />
+    </ItemGroup>
+  </Target>
```

`Directory.Packages.props` adds `Newtonsoft.Json` to central management and the
explanatory comment is updated, because the SDK now references it directly:

```diff
   <ItemGroup Label="Production">
     ...
+    <!-- Internal response-DTO deserialization in the SDK (the generated client's
+         models are Newtonsoft-shaped). The client csproj keeps its own
+         VersionOverride for RestSharp/Polly; Newtonsoft is now shared here.
+         System.ComponentModel.Annotations is needed by the embedded DTOs'
+         IValidatableObject on netstandard2.1. -->
+    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
+    <PackageVersion Include="System.ComponentModel.Annotations" Version="5.0.0" />
   </ItemGroup>
```

The generated types are now `internal` to their assembly and reachable only by
the SDK via `InternalsVisibleTo`, so even though the DLL ships under `lib/{tfm}`
no consumer can reference them, and they never appear in a public signature of
`KyuzanInc.Peak.Sdk`. `$(OutputPath)` is per-TFM, so each packable SDK TFM
(`netstandard2.1`, `net8.0`, `net8.0-windows`) embeds the client DLL compatible
with it; `net8.0-windows` reuses the client's `net8.0` asset.

### 6.2 Transport: pick the serializer by response type

`DefaultPeakHttpClient` keeps serializing requests and deserializing the SDK's
own types with STJ via `PeakJsonContext`. For response type `T`, it deserializes
with Newtonsoft when `T` comes from the generated client assembly:

```csharp
private static readonly System.Reflection.Assembly GeneratedClientAssembly =
    typeof(KyuzanInc.Peak.PublicApiClient.Model.InitOtpLoginResponseDto).Assembly;

private static T? Deserialize<T>(string body) where T : class
{
    if (typeof(T).Assembly == GeneratedClientAssembly)
        return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(body);

    var typeInfo = (JsonTypeInfo<T>?)PeakJsonContext.Default.GetTypeInfo(typeof(T))
        ?? throw new PeakError(PeakErrorCode.InvalidArgument,
            $"Type {typeof(T).Name} is not registered in PeakJsonContext.");
    return JsonSerializer.Deserialize(body, typeInfo);
}
```

The catch in `SendAsync` adds `Newtonsoft.Json.JsonException` alongside the
existing `System.Text.Json.JsonException`, both mapped to
`PeakError(PeakErrorCode.InvalidResponse, ...)`. The exact `application/json`
content type, `X-API-KEY` injection, header handling, and HTTP-error mapping are
unchanged. The public `IPeakHttpClient` signatures are unchanged, so the public
surface and its baseline (Section 6.5) are unaffected.

**Custom transport contract.** A consumer-supplied `IPeakHttpClient` is called
with `T` bound to a generated (now `internal`) response type for adopted
endpoints. External code cannot name that type, but it receives `typeof(T)` and
can deserialize reflectively — `JsonConvert.DeserializeObject(body, typeof(T))` —
without referencing Newtonsoft attributes directly. The default transport does
this through an `internal` `PeakResponseJson` helper; the contract for custom
transports ("deserialize a `KyuzanInc.Peak.PublicApiClient` type with Newtonsoft")
is documented on `IPeakHttpClient`. This is the one consumer-visible behavioral
change and is recorded as R6.

#### 6.2.1 Unknown enum values stay forward-compatible

The generated DTOs decorate each enum (`chainType`, `bitcoinAddressType`,
`sourceType`, `creationMethod`, `deletionStatus`) with a bare
`[JsonConverter(typeof(StringEnumConverter))]`, which **hard-fails**
deserialization on any unrecognized string. Because the spec is synced from the
independently-evolving `KyuzanInc/peak` monorepo, an additive server enum (e.g. a
future `chainType: "aptos"`) would otherwise reject an otherwise-valid response as
`PeakErrorCode.InvalidResponse` — a regression from the pre-generated-client
behavior, where these fields were plain `string?` and an unknown value flowed
through untouched.

`PeakResponseJson.Deserialize<T>` therefore passes a `JsonSerializerSettings`
with a `TolerantEnumContractResolver` for the generated-assembly branch only.
The resolver overrides `CreateProperty` and, for enum (and `Nullable<enum>`)
members, swaps the generated converter for a `TolerantStringEnumConverter` that
returns the enum default (or `null` for a nullable enum) for **any** unrecognized
wire value instead of throwing. "Unrecognized" means an unknown string OR a
non-string token: a numeric token is explicitly **not** coerced to the member
with that ordinal (so `chainType: 1` does not silently become `"evm"`); it, like a
bool or object, is consumed and treated as unknown. This is the **only** reliable
override because a member-level `[JsonConverter]` attribute wins over any
converter in `JsonSerializerSettings.Converters`. The mappers (Section 6.3) render
an undefined enum as `null` on the public `string?` field, so an unknown value
surfaces as `null` ("not recognized").

This is a deliberate trade-off, **not** a full restoration of the prior
`string?` passthrough: the old path preserved the raw unknown string, whereas a
C# enum cannot hold an out-of-set value, so an unknown value is nulled rather than
passed through. The behavior is strictly more lenient than the generated
hard-fail (the whole response no longer fails on an additive server enum) and
never produces a wrong known value; adopting a new server enum value requires
resyncing the spec and regenerating.

The tolerance is scoped strictly to enum members: required-field presence and
numeric (`accountIndex`) validation still **fail closed** as `InvalidResponse`,
since their converters and `IsRequired` handling are untouched. Tests in
`GeneratedDtoMapperTests` cover both directions (known values map through; unknown
`chainType` / `bitcoinAddressType` / `sourceType` / `creationMethod` /
`deletionStatus`, including one nested inside a full login response, deserialize
without throwing and map to `null`).

### 6.3 Mappers (responses only)

Mappers live in `packages/peak-sdk-csharp/src/Mapping/` as `internal` static
extension methods (`ToPublic`). They convert `List<T>` to `T[]` and flatten
nested DTOs. Inventory:

| Endpoint | Response: generated → public |
| --- | --- |
| `auth/otp/init-login` | `InitOtpLoginResponseDto` → `InitOtpLoginResponse` |
| `auth/otp/complete-login` | `CompleteOtpLoginResponseDto` → `CompleteOtpLoginResponse` |
| `accounts/list` | `ListAccountsResponseDto` → `ListAccountsResponse` |
| `accounts/list-addresses` | `ListAccountAddressesResponseDto` → `ListAccountAddressesResponse` |
| `accounts/update-display-name` | `UpdateAccountDisplayNameResponseDto` → `AccountResponse` |
| `accounts/get-address-detail` | `GetAccountAddressWithAccountAndSourceResponseDto` → `GetAddressDetailResponse` |
| `private-keys/init-import` | `InitImportPrivateKeyResponseDto` → `InitImportPrivateKeyResponse` |
| `private-keys/complete-import` | `CompleteImportPrivateKeyResponseDto` → `CompleteImportPrivateKeyResponse` |
| `private-keys/export` | `ExportPrivateKeyResponseDto` → `ExportPrivateKeyResponse` |

Example diff (`AuthService.InitOtpLoginAsync`):

```diff
-  var payload = new InitOtpLoginRequest { Email = email };
-  var response = await httpClient.PostAsync<InitOtpLoginRequest, InitOtpLoginResponse>(
-      "public-api/v1/auth/otp/init-login", payload, cancellationToken: cancellationToken).ConfigureAwait(false);
-  logger.LogInformation("Init OTP login successful - OTP ID: {OtpId}", response?.OtpId);
-  return response;
+  var payload = new InitOtpLoginRequest { Email = email };
+  var dto = await httpClient.PostAsync<InitOtpLoginRequest, InitOtpLoginResponseDto>(
+      "public-api/v1/auth/otp/init-login", payload, cancellationToken: cancellationToken).ConfigureAwait(false);
+  var response = dto?.ToPublic();
+  logger.LogInformation("Init OTP login successful - OTP ID: {OtpId}", response?.OtpId);
+  return response;
```

Endpoint path strings, `Authorization: Bearer {jwt}` headers, the Turnkey stamp
construction, and all public method signatures are unchanged.

### 6.4 Requests, PeakJsonContext, and signed envelopes stay as-is

Requests keep their hand-written DTOs and STJ serialization through
`PeakJsonContext`, for two reasons:

- The 3 private-key request bodies wrap `Turnkey.Http.SignedRequest` and their
  exact wire bytes are locked by `TurnkeyWireFormatSmokeTests`. Newtonsoft-
  serializing the generated request DTOs (which use a different `SignedRequest`
  type and different null/default emission rules) risks changing those bytes.
- Request shapes are small and stable; the drift-prone surface is the responses.

`PeakJsonContext` therefore stays as-is, including the request and result types
it already registers. Request-DTO adoption is recorded as a follow-up (OQ3).

### 6.5 Public surface and the baseline test

`Models/Models.cs` is unchanged: it remains the public surface and the mapping
target. The now-unused public response types are still produced by the mappers,
so they stay referenced; the unused public request-result types are untouched.

D13 is enforced at two layers, so a leak needs both to fail:

1. **The client assembly exposes no public types** (Section 6.1 internalization).
   A test asserts `typeof(InitOtpLoginResponseDto).Assembly.GetExportedTypes()`
   is empty, catching any future generated type the internalize step misses.
2. **The SDK public-surface check.** This PR **adds** a test using
   `PublicApiGenerator` over the `KyuzanInc.Peak.Sdk` assembly that fails if the
   generated namespace `KyuzanInc.Peak.PublicApiClient` appears anywhere on the
   public surface, plus a presence sanity check for the intended surface
   (`PeakClient`, the `Models` namespace, `IPeakHttpClient`). It is the invariant,
   not a byte-for-byte committed snapshot: `PublicApiGenerator` output varies with
   the .NET SDK / Roslyn patch (nullable + compiler attributes), so a pinned
   snapshot is fragile across local vs CI runners. `PublicApiGenerator` is added
   as a test-only package via central management.

### 6.6 Generated client, drift CI, and docs

The generator config and the spec source are unchanged, so the generated DTO
shapes do not move. The one-time diff to the committed client is the
`public`→`internal` flip from the script step (Section 6.1); the
`openapi-client-drift` job reproduces that step and passes once the internalized
client is committed. The client's own dependency set is unchanged, so its
`packages.lock.json` does not change.

Refresh `packages/peak-sdk-csharp/src/packages.lock.json` for the new
`Newtonsoft.Json` and ProjectReference edges, and update `docs/sync-rules.md`
to record that the SDK consumes the client's response models and that generation
now internalizes the output (committed together, locked-mode restore in CI).

## 7. Testing strategy

- **Keep all existing tests green**, especially `TurnkeyWireFormatSmokeTests`
  (requests and signed envelopes are unchanged) and `PeakClientTests` /
  `PeakErrorTests` / `SessionJwtTests`.
- **Response round-trip tests (new):** for each adopted endpoint, feed a
  representative server JSON body through the transport's Newtonsoft path and
  assert the resulting generated DTO has the expected field values, including
  enum string values such as `chainType: "evm"`.
- **Mapping tests (new):** assert each `ToPublic` copies every field, including
  `List<T>` → `T[]` and nested DTOs, from a populated instance.
- **Forward-compat enum tests (new):** Section 6.2.1 — unknown enum strings
  (including one nested in a full login response) deserialize without throwing and
  map to `null`, while non-integral / missing required fields still fail closed as
  `InvalidResponse`.
- **Public-surface baseline test (new):** Section 6.5, including the assertion
  that the generated client assembly exports zero public types.
- **Packaging check:** `dotnet pack` the SDK and assert each packable TFM's
  `lib/` contains `KyuzanInc.Peak.PublicApiClient.dll`, the `.nuspec` lists
  `Newtonsoft.Json` and `System.ComponentModel.Annotations` as dependencies, and
  it does **not** list the generated client. Run per-project locally because the
  full-solution restore 401s without a `read:packages` PAT; CI runs the full
  solution.

## 8. Risks

- **R1 — Newtonsoft on IL2CPP (Unity adapter).** Newtonsoft uses reflection;
  IL2CPP strips unused members. Mitigation: add a `link.xml` in
  `peak-sdk-csharp-unity` preserving the generated `Model` namespace and
  `Newtonsoft.Json`. Tracked as OQ2.
- **R2 — embedded-client transitive dependencies.** The SDK declares
  `Newtonsoft.Json` (response parsing) and `System.ComponentModel.Annotations`
  (the DTOs implement `IValidatableObject`, needed to load them on
  netstandard2.1), because `PrivateAssets=all` hides the client's own copies.
  `RestSharp`/`Polly` are **not** declared: only the client's API/transport types
  use them and the SDK never instantiates those, so the CLR never loads those
  assemblies. The packaging check (Section 7) asserts exactly this dependency
  set. A models-only client (OQ3) removes RestSharp/Polly entirely.
- **R3 — packaging.** A `PrivateAssets=all` ProjectReference to an
  `IsPackable=false` project does not auto-embed its DLL; `BuildOutputInPackage`
  fixes this and the packaging check verifies all three SDK TFMs.
- **R4 — response wire parsing.** Covered by the response round-trip tests
  (Section 7); Newtonsoft honors the generated `[DataMember]` / enum-converter
  attributes natively, which is what those models are generated for.
- **R5 — internalization completeness.** The script step must flip *every*
  generated public type, or a missed type both leaks and breaks accessibility
  consistency. Mitigations: the keyword-anchored transform covers
  class/interface/enum/struct/delegate/record (the committed client includes a
  top-level `public delegate`, the easy one to miss); the client project must
  still compile after the flip (a missed type referenced by a flipped one would
  error); and the exported-types-empty test (Section 6.5) fails loudly if any
  slip through.
- **R6 — custom-transport contract change.** Consumers who inject their own
  `IPeakHttpClient` must deserialize generated response types with Newtonsoft
  (Section 6.2). This is a documented, breaking-ish behavior for that advanced
  scenario; the default transport (the common case) is unaffected. A test covers
  a custom transport that uses the reflective `typeof(T)` path.

## 9. Implementation outline

1. Add the internalize step to `scripts/generate-public-api-client.sh`;
   regenerate; commit the internalized client (the drift-baseline diff). Add the
   hand-authored `AssemblyAttributes.cs` friend-attribute file and its
   `<Compile>` entry to the client project.
2. Add the SDK `ProjectReference` (PrivateAssets=all) + `BuildOutputInPackage`
   target; add `Newtonsoft.Json` to `Directory.Packages.props` and the SDK
   csproj; refresh the SDK lock file.
3. Add the transport `Deserialize<T>` branch and the `Newtonsoft.Json.JsonException`
   mapping in `DefaultPeakHttpClient`; add `PeakResponseJson.Deserialize<T>`.
4. Add the `Mapping/` extensions; rewire `AuthService`, `AccountService`, and the
   response side of `PrivateKeyService` to generated response DTOs.
5. Add the response round-trip, mapping, exported-types-empty, and public-surface
   baseline tests.
6. Update `docs/sync-rules.md`.
7. Verify locally (standalone projects) and rely on CI for the full solution and
   the drift job (which now reproduces the internalize step).

## 10. Open questions (resolved by recommendation; flag if you disagree)

- **OQ1 — scope.** Adopt generated **response** DTOs now; keep requests
  hand-written (Section 6.4). **Recommendation: yes.** If the team prefers no new
  runtime dependency at all, fall back to Option 0 (contract test) instead.
- **OQ2 — Unity `link.xml`.** Add a Newtonsoft/`Model`-preserving `link.xml` in
  the Unity adapter as part of this work. **Recommendation: yes.**
- **OQ3 — follow-ups.** Track (a) request-DTO adoption and (b) slimming the
  client to models-only once a generator-core bump allows clean output.
  **Recommendation: yes, as tracked follow-ups, not in this PR.**
- **OQ4 — baseline test in this PR.** Add the public-surface baseline test now.
  **Recommendation: yes.**

## 11. References

- `plans/plans-peak-sdk-csharp.md:108-134` (D6, D13, D23, D24), `:50` (W0c),
  `:54` (W4).
- `docs/superpowers/plans/2026-05-29-csharp-public-api-client-codegen.md:20-41`
  (restsharp/Newtonsoft facts; Follow-up 1 "Consumer wiring").
- `packages/peak-sdk-csharp/src/Utils/IPeakHttpClient.cs`,
  `Utils/DefaultPeakHttpClient.cs`, `PeakJsonContext.cs`,
  `Services/AuthService.cs`, `Models/Models.cs`.
- `packages/peak-public-api-client-csharp/openapi-config.yaml`,
  `KyuzanInc.Peak.PublicApiClient.csproj`,
  `src/KyuzanInc.Peak.PublicApiClient/Model/*`.
- `CLAUDE.md` (logging/dependency constraints, locked-mode restore);
  `Directory.Packages.props` (central package management note).
```
