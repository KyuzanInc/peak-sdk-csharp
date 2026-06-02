# Consume the generated OpenAPI client — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `KyuzanInc.Peak.Sdk` deserialize peak-server **responses** into the generated client's DTOs and map them to the public System.Text.Json DTOs, so response shapes are spec-driven, without leaking generated types onto the SDK public surface.

**Architecture:** The committed generated client (`KyuzanInc.Peak.PublicApiClient`, Newtonsoft, RestSharp) is made `internal` and referenced privately by the SDK. `DefaultPeakHttpClient` deserializes response types from that assembly with Newtonsoft (everything else stays STJ source-gen). Services map each generated response DTO to the existing public DTO via `internal` `ToPublic()` extensions. Requests, the `IPeakHttpClient` public surface, `PeakJsonContext`, and the signed-envelope path are unchanged.

**Tech Stack:** .NET 8 / netstandard2.1, System.Text.Json (source-gen), Newtonsoft.Json 13.0.3 (internal response parsing), xUnit + FluentAssertions + NSubstitute, PublicApiGenerator, openapi-generator-cli 7.9.0.

**Spec:** `docs/superpowers/specs/2026-06-01-consume-generated-openapi-client-design.md`.

---

## Prerequisites and local constraints (read first)

- **GitHub Packages auth:** `KyuzanInc.Peak.Sdk` depends on `KyuzanInc.Turnkey.Sdk` from GitHub Packages. A full-solution `dotnet restore` 401s without a PAT that has `read:packages` (see `MEMORY.md` / `docs/development.md`). Set up auth, or push and let CI run the full build/test. Build standalone projects locally where possible.
- **Codegen (Task 1) needs Node + a JRE.** `scripts/generate-public-api-client.sh` runs `npm ci` and the pinned 7.9.0 generator jar. No GitHub Packages auth needed for codegen.
- **Verification commands** assume restore works. Locally without a PAT, expect restore to 401; the step still documents the command CI runs.
- All commands run from the repo root: `packages/peak-sdk-csharp/.claude/worktrees/vigorous-khayyam-39b51a` (or your checkout root).
- Repo rule: `Directory.Build.props` sets `TreatWarningsAsErrors=true` and `ImplicitUsings=disable` for product code — new SDK files need explicit `using`s and must be warning-clean.

---

## File structure

**Created:**
- `packages/peak-public-api-client-csharp/AssemblyAttributes.cs` — `InternalsVisibleTo` for the SDK + tests (the client csproj has `GenerateAssemblyInfo=false`, so MSBuild items would be ignored).
- `packages/peak-sdk-csharp/src/Serialization/PeakResponseJson.cs` — `internal` response deserializer that picks Newtonsoft for generated-assembly types, STJ otherwise.
- `packages/peak-sdk-csharp/src/Mapping/GeneratedDtoMappers.cs` — `internal` `ToPublic()` extensions (generated DTO → public DTO).
- `packages/peak-sdk-csharp/tests/GeneratedClientInternalizationTests.cs` — asserts the client assembly exports zero public types.
- `packages/peak-sdk-csharp/tests/PeakResponseJsonTests.cs` — response round-trip through the Newtonsoft path.
- `packages/peak-sdk-csharp/tests/GeneratedDtoMapperTests.cs` — mapping correctness (enum→wire, decimal→int, list→array, nested).
- `packages/peak-sdk-csharp/tests/PublicSurfaceBaselineTests.cs` + `PublicApi.Sdk.approved.txt` — PublicApiGenerator baseline (no generated types leak).

**Modified:**
- `scripts/generate-public-api-client.sh` — append the internalize step.
- `packages/peak-public-api-client-csharp/KyuzanInc.Peak.PublicApiClient.csproj` — compile `AssemblyAttributes.cs`.
- `packages/peak-public-api-client-csharp/src/**` — regenerated (the `public`→`internal` diff).
- `Directory.Packages.props` — add `Newtonsoft.Json` (production) and `PublicApiGenerator` (test).
- `packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj` — `Newtonsoft.Json`, private `ProjectReference`, `BuildOutputInPackage` target.
- `packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj` — direct client `ProjectReference` (to name internal types in tests) + `PublicApiGenerator`.
- `packages/peak-sdk-csharp/src/Utils/DefaultPeakHttpClient.cs` — call `PeakResponseJson` + catch `Newtonsoft.Json.JsonException`.
- `packages/peak-sdk-csharp/src/Services/{AuthService,AccountService,PrivateKeyService}.cs` — response types → generated DTOs + map.
- `packages/peak-sdk-csharp/src/packages.lock.json` — refreshed.
- `docs/sync-rules.md` — note the consumer wiring + internalization.

---

## Task 1: Internalize the generated client

**Files:**
- Modify: `scripts/generate-public-api-client.sh`
- Create: `packages/peak-public-api-client-csharp/AssemblyAttributes.cs`
- Modify: `packages/peak-public-api-client-csharp/KyuzanInc.Peak.PublicApiClient.csproj`
- Regenerate: `packages/peak-public-api-client-csharp/src/**`

- [ ] **Step 1: Add the internalize step to the generation script**

Open `scripts/generate-public-api-client.sh`. After the generator-version check block (the `if [ "$ACTUAL_VERSION" != "$EXPECTED_VERSION" ]` block) and before the final `echo "Generated ..."`, insert:

```bash
# Make every generated top-level type internal so the embedded client DLL
# exposes no public API to SDK consumers (D13). Deterministic + portable
# (perl, not GNU/BSD-specific `sed -i`), so the drift job reproduces it.
find "$PKG/src" -name '*.cs' -print0 \
  | xargs -0 perl -i -pe 's/^(\s*)public( (?:partial |sealed |abstract |static )*(?:class|interface|enum|struct|delegate|record)\b)/$1internal$2/'
```

- [ ] **Step 2: Create the friend-attribute file**

Create `packages/peak-public-api-client-csharp/AssemblyAttributes.cs`:

```csharp
using System.Runtime.CompilerServices;

// The client csproj sets GenerateAssemblyInfo=false, so MSBuild <InternalsVisibleTo>
// items are ignored. Emit the attributes explicitly. This file lives OUTSIDE the
// regenerated src/ tree, so the generation script and drift CI never touch it.
[assembly: InternalsVisibleTo("KyuzanInc.Peak.Sdk")]
[assembly: InternalsVisibleTo("KyuzanInc.Peak.Sdk.Tests")]
```

- [ ] **Step 3: Compile the friend-attribute file**

In `packages/peak-public-api-client-csharp/KyuzanInc.Peak.PublicApiClient.csproj`, change the compile ItemGroup:

```diff
   <ItemGroup>
     <Compile Include="src/KyuzanInc.Peak.PublicApiClient/**/*.cs" />
+    <Compile Include="AssemblyAttributes.cs" />
   </ItemGroup>
```

- [ ] **Step 4: Regenerate the client (applies the internalize step)**

Run: `bash scripts/generate-public-api-client.sh`
Expected: ends with `Generated C# client into .../ (generator 7.9.0)` and no error.

- [ ] **Step 5: Verify no public top-level type remains**

Run: `grep -rnE '^\s*public (partial |sealed |abstract |static )*(class|interface|enum|struct|delegate|record)\b' packages/peak-public-api-client-csharp/src/ | wc -l`
Expected: `0`

Run: `grep -rn 'internal delegate Exception ExceptionFactory' packages/peak-public-api-client-csharp/src/KyuzanInc.Peak.PublicApiClient/Client/ExceptionFactory.cs`
Expected: one match (the delegate was internalized).

- [ ] **Step 6: Build the client standalone to confirm it still compiles**

Run: `dotnet build packages/peak-public-api-client-csharp/KyuzanInc.Peak.PublicApiClient.csproj -c Release`
Expected: `Build succeeded` (no GitHub Packages auth needed — this project has no Turnkey dependency).

- [ ] **Step 7: Commit**

```bash
git add scripts/generate-public-api-client.sh packages/peak-public-api-client-csharp/
git commit -m "port: internalize generated OpenAPI client for private SDK consumption"
```

---

## Task 2: Wire the SDK and tests to the internal client

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj`
- Modify: `packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj`
- Modify: `packages/peak-sdk-csharp/src/packages.lock.json` (regenerated by restore)

- [ ] **Step 1: Add Newtonsoft.Json and PublicApiGenerator to central management**

In `Directory.Packages.props`, add to the `Production` ItemGroup:

```diff
     <PackageVersion Include="System.Security.Cryptography.ProtectedData" Version="8.0.0" />
+    <!-- Internal response-DTO deserialization in the SDK (the generated client's
+         models are Newtonsoft-shaped). The client csproj keeps its own
+         VersionOverride for RestSharp/Polly; Newtonsoft is shared here. -->
+    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
+    <!-- The embedded generated DTOs implement IValidatableObject from
+         System.ComponentModel.DataAnnotations; netstandard2.1 consumers need this
+         assembly to load them, and PrivateAssets hides the client's own copy. -->
+    <PackageVersion Include="System.ComponentModel.Annotations" Version="5.0.0" />
   </ItemGroup>
```

and to the `Test` ItemGroup:

```diff
     <PackageVersion Include="FsCheck.Xunit" Version="3.3.3" />
+    <PackageVersion Include="PublicApiGenerator" Version="11.4.5" />
   </ItemGroup>
```

- [ ] **Step 2: Reference the client privately from the SDK + embed its DLL**

In `packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj`, add `Newtonsoft.Json` to the production references, and add the private ProjectReference + packaging target:

```diff
   <ItemGroup>
     <PackageReference Include="KyuzanInc.Turnkey.Sdk" />
     <PackageReference Include="System.Text.Json" />
     <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
     <PackageReference Include="Microsoft.Extensions.Http" />
+    <PackageReference Include="Newtonsoft.Json" />
+    <!-- Required at runtime by the embedded generated DTOs (IValidatableObject)
+         for netstandard2.1 consumers; the client's own reference is hidden by
+         PrivateAssets=all below. -->
+    <PackageReference Include="System.ComponentModel.Annotations" />
   </ItemGroup>

+  <!--
+    Internal generated DTO client (D13). PrivateAssets=all keeps it off the
+    consumer's package dependency graph; the DLL is embedded into the nupkg by
+    the target below so it loads at runtime (a ProjectReference to an
+    IsPackable=false project is otherwise omitted from the package). The client's
+    types are internal, so the lib/ DLL exposes no public API to reference.
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

- [ ] **Step 3: Reference the client directly from the test project**

The SDK→client reference uses `PrivateAssets=all`, so the client types do **not** flow to the test project transitively. Tests that name `KyuzanInc.Peak.PublicApiClient.Model.*` (internal) need a direct reference; `InternalsVisibleTo("KyuzanInc.Peak.Sdk.Tests")` (Task 1) grants access. In `packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj`:

```diff
   <ItemGroup>
     <ProjectReference Include="..\src\peak-sdk-csharp.csproj" />
+    <ProjectReference Include="..\..\peak-public-api-client-csharp\KyuzanInc.Peak.PublicApiClient.csproj" />
   </ItemGroup>

   <ItemGroup>
     <PackageReference Include="Microsoft.NET.Test.Sdk" />
     <PackageReference Include="xunit" />
     <PackageReference Include="xunit.runner.visualstudio">
       <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
       <PrivateAssets>all</PrivateAssets>
     </PackageReference>
     <PackageReference Include="NSubstitute" />
     <PackageReference Include="FluentAssertions" />
+    <PackageReference Include="PublicApiGenerator" />
     <PackageReference Include="coverlet.collector">
       <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
       <PrivateAssets>all</PrivateAssets>
     </PackageReference>
   </ItemGroup>
```

- [ ] **Step 4: Restore to refresh BOTH lock files, then build the SDK**

Restore the whole solution so both lock files update: `src/packages.lock.json` (the `Newtonsoft.Json` + client edges) and `tests/packages.lock.json` (the `PublicApiGenerator` + client `ProjectReference` edges). Committing only the src lock would leave the test lock stale and break locked-mode restore for the test project.

Run: `dotnet restore peak-sdk-csharp.sln`
Expected: restore succeeds (with GitHub Packages auth) and rewrites both `packages/peak-sdk-csharp/src/packages.lock.json` and `packages/peak-sdk-csharp/tests/packages.lock.json`. Without a PAT this 401s on the Turnkey package — run in CI instead.

Run: `dotnet build packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj -c Release`
Expected: `Build succeeded` — the SDK compiles against the internal client (no code uses it yet; this proves the references resolve).

- [ ] **Step 5: Commit**

```bash
git add Directory.Packages.props \
  packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj \
  packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj \
  packages/peak-sdk-csharp/src/packages.lock.json \
  packages/peak-sdk-csharp/tests/packages.lock.json
git commit -m "feat: reference internal generated client from the SDK (private assets)"
```

---

## Task 3: Test that the client assembly leaks no public types (D13 layer 1)

**Files:**
- Test: `packages/peak-sdk-csharp/tests/GeneratedClientInternalizationTests.cs`

- [ ] **Step 1: Write the test**

Create `packages/peak-sdk-csharp/tests/GeneratedClientInternalizationTests.cs`:

```csharp
using FluentAssertions;
using Xunit;
using Gen = KyuzanInc.Peak.PublicApiClient.Model;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class GeneratedClientInternalizationTests
    {
        [Fact]
        public void GeneratedClientAssembly_ExportsNoPublicTypes()
        {
            // D13: the embedded client DLL must expose no public surface, so
            // consumers cannot reference generated types even though it ships
            // under lib/{tfm}. GetExportedTypes() returns only public types.
            var assembly = typeof(Gen.InitOtpLoginResponseDto).Assembly;
            assembly.GetExportedTypes().Should().BeEmpty();
        }
    }
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~GeneratedClientInternalizationTests"`
Expected: PASS (Task 1 internalized every type). If it FAILS listing a type, the internalize transform missed a kind — add that kind to the perl regex in `scripts/generate-public-api-client.sh`, regenerate, and re-run.

- [ ] **Step 3: Commit**

```bash
git add packages/peak-sdk-csharp/tests/GeneratedClientInternalizationTests.cs
git commit -m "test: assert generated client assembly exports no public types"
```

---

## Task 4: Response deserializer + transport Newtonsoft path

**Files:**
- Create: `packages/peak-sdk-csharp/src/Serialization/PeakResponseJson.cs`
- Modify: `packages/peak-sdk-csharp/src/Utils/DefaultPeakHttpClient.cs`
- Test: `packages/peak-sdk-csharp/tests/PeakResponseJsonTests.cs`

- [ ] **Step 1: Write the failing round-trip test**

Create `packages/peak-sdk-csharp/tests/PeakResponseJsonTests.cs`:

```csharp
using FluentAssertions;
using Xunit;
using Gen = KyuzanInc.Peak.PublicApiClient.Model;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class PeakResponseJsonTests
    {
        [Fact]
        public void Deserialize_GeneratedDto_ReadsCamelCaseFields()
        {
            const string json = "{\"otpId\":\"otp-123\"}";
            var dto = PeakResponseJson.Deserialize<Gen.InitOtpLoginResponseDto>(json);
            dto.Should().NotBeNull();
            dto!.OtpId.Should().Be("otp-123");
        }

        [Fact]
        public void Deserialize_GeneratedDto_ParsesStringEnums()
        {
            const string json =
                "{\"id\":\"a1\",\"accountId\":\"acc1\",\"address\":\"0xabc\",\"chainType\":\"evm\"}";
            var dto = PeakResponseJson.Deserialize<Gen.AccountAddressResponseDto>(json);
            dto.Should().NotBeNull();
            dto!.ChainType.Should().Be(Gen.ChainTypeEnum.Evm);
        }
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~PeakResponseJsonTests"`
Expected: FAIL to compile — `PeakResponseJson` does not exist yet.

- [ ] **Step 3: Implement the deserializer**

Create `packages/peak-sdk-csharp/src/Serialization/PeakResponseJson.cs`:

```csharp
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace KyuzanInc.Peak.Sdk
{
    /// <summary>
    /// Deserializes HTTP response bodies. Types from the internal generated
    /// client assembly are Newtonsoft-shaped (Section 3 of the spec), so they go
    /// through Newtonsoft; the SDK's own types keep using STJ source generation.
    /// </summary>
    internal static class PeakResponseJson
    {
        private static readonly Assembly GeneratedClientAssembly =
            typeof(KyuzanInc.Peak.PublicApiClient.Model.InitOtpLoginResponseDto).Assembly;

        internal static T? Deserialize<T>(string body) where T : class
        {
            if (typeof(T).Assembly == GeneratedClientAssembly)
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(body);
            }

            var typeInfo = (JsonTypeInfo<T>?)PeakJsonContext.Default.GetTypeInfo(typeof(T))
                ?? throw new PeakError(PeakErrorCode.InvalidArgument,
                    $"Type {typeof(T).Name} is not registered in PeakJsonContext.");
            return JsonSerializer.Deserialize(body, typeInfo);
        }
    }
}
```

- [ ] **Step 4: Route the transport response path through it**

In `packages/peak-sdk-csharp/src/Utils/DefaultPeakHttpClient.cs`, inside `SendAsync<T>`, replace the inline deserialize block:

```diff
                 if (string.IsNullOrEmpty(body)) return null;

-                var typeInfo = (JsonTypeInfo<T>?)PeakJsonContext.Default.GetTypeInfo(typeof(T));
-                if (typeInfo is null)
-                {
-                    throw new PeakError(PeakErrorCode.InvalidArgument,
-                        $"Type {typeof(T).Name} is not registered in PeakJsonContext.");
-                }
-                return JsonSerializer.Deserialize(body!, typeInfo);
+                return PeakResponseJson.Deserialize<T>(body!);
```

and add a Newtonsoft parse-error catch next to the existing System.Text.Json one:

```diff
             catch (JsonException ex)
             {
                 throw new PeakError(PeakErrorCode.InvalidResponse,
                     $"Failed to parse JSON response: {ex.Message}", ex,
                     new ApiResponseContext
                     {
                         HttpStatusCode = response != null ? (int)response.StatusCode : (int?)null,
                         Endpoint = req.RequestUri?.AbsolutePath,
                         Method = req.Method.Method,
                         RawResponseBody = body,
                     });
             }
+            catch (Newtonsoft.Json.JsonException ex)
+            {
+                throw new PeakError(PeakErrorCode.InvalidResponse,
+                    $"Failed to parse JSON response: {ex.Message}", ex,
+                    new ApiResponseContext
+                    {
+                        HttpStatusCode = response != null ? (int)response.StatusCode : (int?)null,
+                        Endpoint = req.RequestUri?.AbsolutePath,
+                        Method = req.Method.Method,
+                        RawResponseBody = body,
+                    });
+            }
```

Note: `SendAsync` keeps its other `using`s; `JsonTypeInfo`/`JsonSerializer` are still used by `PostAsync` (request serialization), so no using becomes unused (which would break `TreatWarningsAsErrors`).

Also document the R6 custom-transport contract on the interface (a doc comment only, so the public-surface baseline is unaffected). In `packages/peak-sdk-csharp/src/Utils/IPeakHttpClient.cs`:

```diff
     /// or platform-specific retry policies.
     /// </summary>
+    /// <remarks>
+    /// For endpoints whose response type comes from the internal
+    /// <c>KyuzanInc.Peak.PublicApiClient</c> assembly, a custom implementation
+    /// must deserialize the body with Newtonsoft.Json (for example
+    /// <c>JsonConvert.DeserializeObject(body, typeof(T))</c>) because those types
+    /// are Newtonsoft-shaped. The default <see cref="DefaultPeakHttpClient"/>
+    /// does this automatically.
+    /// </remarks>
     public interface IPeakHttpClient
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~PeakResponseJsonTests"`
Expected: PASS (both facts).

- [ ] **Step 6: Commit**

```bash
git add packages/peak-sdk-csharp/src/Serialization/PeakResponseJson.cs packages/peak-sdk-csharp/src/Utils/DefaultPeakHttpClient.cs packages/peak-sdk-csharp/tests/PeakResponseJsonTests.cs
git commit -m "feat: deserialize generated response DTOs with Newtonsoft in the transport"
```

---

## Task 5: Generated-DTO → public-DTO mappers

**Files:**
- Create: `packages/peak-sdk-csharp/src/Mapping/GeneratedDtoMappers.cs`
- Test: `packages/peak-sdk-csharp/tests/GeneratedDtoMapperTests.cs`

- [ ] **Step 1: Write the failing mapping tests**

Create `packages/peak-sdk-csharp/tests/GeneratedDtoMapperTests.cs`:

```csharp
using System;
using FluentAssertions;
using Xunit;
using KyuzanInc.Peak.Sdk.Mapping;
using Gen = KyuzanInc.Peak.PublicApiClient.Model;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class GeneratedDtoMapperTests
    {
        [Fact]
        public void AccountAddress_MapsEnumsToWireStrings_AndKeepsScalars()
        {
            const string json =
                "{\"id\":\"a1\",\"accountId\":\"acc1\",\"address\":\"0xabc\"," +
                "\"chainType\":\"evm\",\"bitcoinAddressType\":\"p2wpkh\"}";
            var dto = PeakResponseJson.Deserialize<Gen.AccountAddressResponseDto>(json)!;

            var pub = dto.ToPublic();

            pub.Id.Should().Be("a1");
            pub.Address.Should().Be("0xabc");
            pub.ChainType.Should().Be("evm");
            pub.BitcoinAddressType.Should().Be("p2wpkh");
        }

        [Fact]
        public void Account_MapsDecimalIndexToInt()
        {
            // The generated DTO marks id/userId/accountSourceId/accountIndex/
            // originProjectId as [DataMember(IsRequired = true)]; Newtonsoft throws
            // if any required field is absent, so fixtures include all of them.
            const string json =
                "{\"id\":\"acc1\",\"userId\":\"u1\",\"accountSourceId\":\"src1\"," +
                "\"accountIndex\":3,\"originProjectId\":\"proj1\"}";
            var dto = PeakResponseJson.Deserialize<Gen.AccountResponseDto>(json)!;

            dto.ToPublic().AccountIndex.Should().Be(3);
        }

        [Fact]
        public void Account_RejectsNonIntegralIndex()
        {
            const string json =
                "{\"id\":\"acc1\",\"userId\":\"u1\",\"accountSourceId\":\"src1\"," +
                "\"accountIndex\":1.5,\"originProjectId\":\"proj1\"}";
            var dto = PeakResponseJson.Deserialize<Gen.AccountResponseDto>(json)!;

            Action act = () => dto.ToPublic();
            act.Should().Throw<PeakError>().Which.Code.Should().Be(PeakErrorCode.InvalidResponse);
        }

        [Fact]
        public void AccountSource_MapsSourceTypeToWireString()
        {
            const string json =
                "{\"id\":\"s1\",\"userId\":\"u1\",\"originProjectId\":\"proj1\"," +
                "\"turnkeyResourceId\":\"tk1\",\"sourceType\":\"private-key\",\"creationMethod\":\"imported\"}";
            var dto = PeakResponseJson.Deserialize<Gen.AccountSourceResponseDto>(json)!;

            var pub = dto.ToPublic();

            pub.SourceType.Should().Be("private-key");
            pub.CreationMethod.Should().Be("imported");
        }

        [Fact]
        public void CompleteOtpLogin_MapsNestedAndList()
        {
            const string json =
                "{\"user\":{\"id\":\"u1\",\"email\":\"a@b.c\",\"originProjectId\":\"proj1\"," +
                "\"turnkeySubOrgId\":\"sub1\",\"turnkeyRootUserId\":\"root1\",\"deletionStatus\":\"none\"}," +
                "\"sessionJwt\":\"jwt\",\"isNewUser\":true," +
                "\"accountAddresses\":[{\"id\":\"ad1\",\"accountId\":\"acc1\",\"address\":\"0xaddr\",\"chainType\":\"solana\"}]}";
            var dto = PeakResponseJson.Deserialize<Gen.CompleteOtpLoginResponseDto>(json)!;

            var pub = dto.ToPublic();

            pub.SessionJwt.Should().Be("jwt");
            pub.IsNewUser.Should().BeTrue();
            pub.User!.Email.Should().Be("a@b.c");
            pub.User!.DeletionStatus.Should().Be("none");
            pub.AccountAddresses.Should().ContainSingle();
            pub.AccountAddresses![0].ChainType.Should().Be("solana");
        }
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~GeneratedDtoMapperTests"`
Expected: FAIL to compile — `ToPublic` / namespace `KyuzanInc.Peak.Sdk.Mapping` do not exist yet.

- [ ] **Step 3: Implement the mappers**

Create `packages/peak-sdk-csharp/src/Mapping/GeneratedDtoMappers.cs`:

```csharp
using System;
using System.Linq;
using KyuzanInc.Peak.Sdk.Models;
using Gen = KyuzanInc.Peak.PublicApiClient.Model;

namespace KyuzanInc.Peak.Sdk.Mapping
{
    /// <summary>
    /// Maps internal generated response DTOs to the public System.Text.Json DTOs.
    /// Generated enums become their wire strings, decimal indexes become int, and
    /// List&lt;T&gt; becomes T[]. Public DTO fields with no spec source (e.g.
    /// UserResponse.IsAuthenticated — not in the spec) are left at default.
    /// </summary>
    internal static class GeneratedDtoMappers
    {
        // Convert a generated enum to its wire string via the [EnumMember] value
        // that the generated [JsonConverter(StringEnumConverter)] uses. Undefined
        // values (e.g. an absent, default(0) enum) map to null, matching the old
        // "field absent -> null string" behavior.
        private static string? ToWire<TEnum>(TEnum value) where TEnum : struct, Enum =>
            Enum.IsDefined(typeof(TEnum), value)
                ? Newtonsoft.Json.JsonConvert.SerializeObject(value).Trim('"')
                : null;

        // The spec types accountIndex as `number` (the generated DTO is decimal),
        // but the public DTO is int. Reject non-integral or out-of-range values as
        // PeakErrorCode.InvalidResponse rather than silently truncating or letting
        // an OverflowException escape — matching how the old STJ-into-int path
        // rejected bad numbers.
        private static int ToAccountIndex(decimal value)
        {
            if (decimal.Truncate(value) != value || value < int.MinValue || value > int.MaxValue)
            {
                throw new PeakError(PeakErrorCode.InvalidResponse,
                    $"accountIndex '{value}' is not a valid 32-bit integer");
            }
            return (int)value;
        }

        // --- entities ---

        internal static UserResponse ToPublic(this Gen.UserResponseDto d) => new()
        {
            Id = d.Id,
            Email = d.Email,
            OriginProjectId = d.OriginProjectId,
            TurnkeySubOrgId = d.TurnkeySubOrgId,
            TurnkeyRootUserId = d.TurnkeyRootUserId,
            DeletionStatus = ToWire(d.DeletionStatus),
            // IsAuthenticated has no spec field; left default(false).
        };

        internal static AccountResponse ToPublic(this Gen.AccountResponseDto d) => new()
        {
            Id = d.Id,
            UserId = d.UserId,
            AccountSourceId = d.AccountSourceId,
            AccountIndex = ToAccountIndex(d.AccountIndex),
            OriginProjectId = d.OriginProjectId,
            DisplayName = d.DisplayName,
        };

        internal static AccountAddressResponse ToPublic(this Gen.AccountAddressResponseDto d) => new()
        {
            Id = d.Id,
            AccountId = d.AccountId,
            Address = d.Address,
            ChainType = ToWire(d.ChainType),
            BitcoinAddressType = d.BitcoinAddressType is { } bt ? ToWire(bt) : null,
        };

        internal static AccountSourceResponse ToPublic(this Gen.AccountSourceResponseDto d) => new()
        {
            Id = d.Id,
            UserId = d.UserId,
            OriginProjectId = d.OriginProjectId,
            SourceType = ToWire(d.SourceType),
            CreationMethod = ToWire(d.CreationMethod),
            TurnkeyResourceId = d.TurnkeyResourceId,
            DisplayName = d.DisplayName,
        };

        // --- responses ---

        internal static InitOtpLoginResponse ToPublic(this Gen.InitOtpLoginResponseDto d) => new()
        {
            OtpId = d.OtpId,
        };

        internal static CompleteOtpLoginResponse ToPublic(this Gen.CompleteOtpLoginResponseDto d) => new()
        {
            User = d.User?.ToPublic(),
            SessionJwt = d.SessionJwt,
            IsNewUser = d.IsNewUser,
            AccountSource = d.AccountSource?.ToPublic(),
            Account = d.Account?.ToPublic(),
            AccountAddresses = d.AccountAddresses?.Select(a => a.ToPublic()).ToArray(),
        };

        internal static ListAccountsResponse ToPublic(this Gen.ListAccountsResponseDto d) => new()
        {
            Accounts = d.Accounts?.Select(a => a.ToPublic()).ToArray(),
        };

        internal static ListAccountAddressesResponse ToPublic(this Gen.ListAccountAddressesResponseDto d) => new()
        {
            AccountAddresses = d.AccountAddresses?.Select(a => a.ToPublic()).ToArray(),
        };

        internal static GetAddressDetailResponse ToPublic(this Gen.GetAccountAddressWithAccountAndSourceResponseDto d) => new()
        {
            AccountAddress = d.AccountAddress?.ToPublic(),
            Account = d.Account?.ToPublic(),
            AccountSource = d.AccountSource?.ToPublic(),
        };

        internal static InitImportPrivateKeyResponse ToPublic(this Gen.InitImportPrivateKeyResponseDto d) => new()
        {
            ImportBundle = d.ImportBundle,
        };

        internal static CompleteImportPrivateKeyResponse ToPublic(this Gen.CompleteImportPrivateKeyResponseDto d) => new()
        {
            Account = d.Account?.ToPublic(),
            AccountAddress = d.AccountAddress?.ToPublic(),
            AccountSource = d.AccountSource?.ToPublic(),
        };

        internal static ExportPrivateKeyResponse ToPublic(this Gen.ExportPrivateKeyResponseDto d) => new()
        {
            ExportBundle = d.ExportBundle,
        };
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~GeneratedDtoMapperTests"`
Expected: PASS (all four facts).

- [ ] **Step 5: Commit**

```bash
git add packages/peak-sdk-csharp/src/Mapping/GeneratedDtoMappers.cs packages/peak-sdk-csharp/tests/GeneratedDtoMapperTests.cs
git commit -m "feat: map generated response DTOs to public DTOs"
```

---

## Task 6: Rewire AuthService to generated response DTOs

**Files:**
- Modify: `packages/peak-sdk-csharp/src/Services/AuthService.cs`
- Test: `packages/peak-sdk-csharp/tests/AuthServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `packages/peak-sdk-csharp/tests/AuthServiceTests.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KyuzanInc.Peak.Sdk.Models;
using KyuzanInc.Peak.Sdk.Services;
using KyuzanInc.Peak.Sdk.Utils;
using NSubstitute;
using Xunit;
using Gen = KyuzanInc.Peak.PublicApiClient.Model;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class AuthServiceTests
    {
        [Fact]
        public async Task InitOtpLogin_MapsGeneratedDtoToPublic()
        {
            var http = Substitute.For<IPeakHttpClient>();
            // Build the generated DTO by deserializing JSON (the protected
            // parameterless ctor), avoiding the required-arg ctor's null checks.
            var dto = PeakResponseJson.Deserialize<Gen.InitOtpLoginResponseDto>("{\"otpId\":\"otp-123\"}")!;
            http.PostAsync<InitOtpLoginRequest, Gen.InitOtpLoginResponseDto>(
                    "public-api/v1/auth/otp/init-login",
                    Arg.Any<InitOtpLoginRequest>(),
                    Arg.Any<IReadOnlyDictionary<string, string>>(),
                    Arg.Any<CancellationToken>())
                .Returns(dto);

            var svc = new AuthService("https://api.peak.xyz", "k", http);
            var result = await svc.InitOtpLoginAsync("a@b.c");

            result!.OtpId.Should().Be("otp-123");
        }

        // R6: a custom transport that knows nothing about generated types and
        // deserializes any response reflectively with Newtonsoft still works.
        private sealed class ReflectiveTransport : IPeakHttpClient
        {
            private readonly string body;
            public ReflectiveTransport(string body) => this.body = body;

            public Task<T?> GetAsync<T>(string endpoint, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default) where T : class =>
                Task.FromResult((T?)Newtonsoft.Json.JsonConvert.DeserializeObject(body, typeof(T)));

            public Task<T?> PostAsync<TBody, T>(string endpoint, TBody payload, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default) where TBody : class where T : class =>
                Task.FromResult((T?)Newtonsoft.Json.JsonConvert.DeserializeObject(body, typeof(T)));

            public Task<T?> PostAsync<T>(string endpoint, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default) where T : class =>
                Task.FromResult((T?)Newtonsoft.Json.JsonConvert.DeserializeObject(body, typeof(T)));
        }

        [Fact]
        public async Task CustomTransport_DeserializesGeneratedDto_Reflectively()
        {
            var svc = new AuthService("https://api.peak.xyz", "k",
                new ReflectiveTransport("{\"otpId\":\"otp-xyz\"}"));
            var result = await svc.InitOtpLoginAsync("a@b.c");
            result!.OtpId.Should().Be("otp-xyz");
        }
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~AuthServiceTests"`
Expected: FAIL — `AuthService` still calls `PostAsync<InitOtpLoginRequest, InitOtpLoginResponse>`, so the mock (set up for `...ResponseDto`) returns null and `OtpId` assertion fails.

- [ ] **Step 3: Rewire the two methods**

In `packages/peak-sdk-csharp/src/Services/AuthService.cs`, add usings under the existing ones:

```diff
 using KyuzanInc.Peak.Sdk.Models;
 using KyuzanInc.Peak.Sdk.Utils;
 using Microsoft.Extensions.Logging;
+using KyuzanInc.Peak.Sdk.Mapping;
+using Gen = KyuzanInc.Peak.PublicApiClient.Model;
```

Replace the body of `InitOtpLoginAsync` with:

```csharp
            logger.LogInformation("Starting init OTP login for: {Email}", email);
            var payload = new InitOtpLoginRequest { Email = email };
            var dto = await httpClient.PostAsync<InitOtpLoginRequest, Gen.InitOtpLoginResponseDto>(
                "public-api/v1/auth/otp/init-login", payload, cancellationToken: cancellationToken).ConfigureAwait(false);
            var response = dto?.ToPublic();
            logger.LogInformation("Init OTP login successful - OTP ID: {OtpId}", response?.OtpId);
            return response;
```

In `CompleteOtpLoginAsync`, change the request/mapping (the `keyPair` block above is unchanged):

```csharp
            var dto = await httpClient.PostAsync<CompleteOtpLoginRequest, Gen.CompleteOtpLoginResponseDto>(
                "public-api/v1/auth/otp/complete-login", payload, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (dto == null)
            {
                throw new PeakError(PeakErrorCode.AuthenticationFailed,
                    "Complete OTP login response was empty");
            }

            var response = dto.ToPublic();
            logger.LogInformation("Complete OTP login successful (isNewUser={IsNewUser})", response.IsNewUser);

            return new CompleteOtpLoginResult
            {
                User = response.User,
                SessionJwt = response.SessionJwt,
                IsNewUser = response.IsNewUser,
                AccountSource = response.AccountSource,
                Account = response.Account,
                AccountAddresses = response.AccountAddresses,
                KeyPair = new KeyPair
                {
                    PrivateKey = keyPair.PrivateKey,
                    PublicKey = keyPair.PublicKey,
                },
            };
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~AuthServiceTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add packages/peak-sdk-csharp/src/Services/AuthService.cs packages/peak-sdk-csharp/tests/AuthServiceTests.cs
git commit -m "feat: AuthService consumes generated response DTOs"
```

---

## Task 7: Rewire AccountService to generated response DTOs

**Files:**
- Modify: `packages/peak-sdk-csharp/src/Services/AccountService.cs`
- Test: `packages/peak-sdk-csharp/tests/AccountServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `packages/peak-sdk-csharp/tests/AccountServiceTests.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KyuzanInc.Peak.Sdk.Models;
using KyuzanInc.Peak.Sdk.Services;
using KyuzanInc.Peak.Sdk.Utils;
using NSubstitute;
using Xunit;
using Gen = KyuzanInc.Peak.PublicApiClient.Model;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class AccountServiceTests
    {
        [Fact]
        public async Task ListAccounts_MapsGeneratedDtos()
        {
            var http = Substitute.For<IPeakHttpClient>();
            // Generated AccountResponseDto requires id/userId/accountSourceId/
            // accountIndex/originProjectId (Newtonsoft throws on a missing required
            // field), so the nested fixture supplies all of them.
            var dto = PeakResponseJson.Deserialize<Gen.ListAccountsResponseDto>(
                "{\"accounts\":[{\"id\":\"acc1\",\"userId\":\"u1\",\"accountSourceId\":\"src1\"," +
                "\"accountIndex\":2,\"originProjectId\":\"proj1\"}]}")!;
            http.GetAsync<Gen.ListAccountsResponseDto>(
                    "public-api/v1/accounts/list", Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(dto);

            var svc = new AccountService("https://api.peak.xyz", "k", "jwt", http);
            var accounts = await svc.ListAccountsAsync();

            accounts.Should().ContainSingle();
            accounts[0].Id.Should().Be("acc1");
            accounts[0].AccountIndex.Should().Be(2);
        }

        [Fact]
        public async Task UpdateDisplayName_MapsNestedAccount()
        {
            var http = Substitute.For<IPeakHttpClient>();
            var dto = PeakResponseJson.Deserialize<Gen.UpdateAccountDisplayNameResponseDto>(
                "{\"account\":{\"id\":\"acc1\",\"userId\":\"u1\",\"accountSourceId\":\"src1\"," +
                "\"accountIndex\":0,\"originProjectId\":\"proj1\",\"displayName\":\"new name\"}}")!;
            http.PostAsync<UpdateAccountDisplayNameRequest, Gen.UpdateAccountDisplayNameResponseDto>(
                    "public-api/v1/accounts/update-display-name",
                    Arg.Any<UpdateAccountDisplayNameRequest>(),
                    Arg.Any<IReadOnlyDictionary<string, string>>(),
                    Arg.Any<CancellationToken>())
                .Returns(dto);

            var svc = new AccountService("https://api.peak.xyz", "k", "jwt", http);
            var account = await svc.UpdateAccountDisplayNameAsync("acc1", "new name");

            account!.Id.Should().Be("acc1");
            account.DisplayName.Should().Be("new name");
        }
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~AccountServiceTests"`
Expected: FAIL to compile — the methods still use the public response types, so the `Gen.*` mock setups do not match their signatures.

- [ ] **Step 3: Rewire the four methods**

In `packages/peak-sdk-csharp/src/Services/AccountService.cs`, add usings:

```diff
 using KyuzanInc.Peak.Sdk.Models;
 using KyuzanInc.Peak.Sdk.Utils;
+using KyuzanInc.Peak.Sdk.Mapping;
+using Gen = KyuzanInc.Peak.PublicApiClient.Model;
```

Replace the four method bodies:

```csharp
        public async Task<AccountResponse[]> ListAccountsAsync(CancellationToken cancellationToken = default)
        {
            var dto = await httpClient.GetAsync<Gen.ListAccountsResponseDto>(
                "public-api/v1/accounts/list", CreateAuthHeaders(), cancellationToken).ConfigureAwait(false);
            return dto?.ToPublic().Accounts ?? Array.Empty<AccountResponse>();
        }

        public async Task<AccountAddressResponse[]> ListAccountAddressesAsync(string accountId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new PeakError(PeakErrorCode.InvalidArgument, "Account ID is required");
            }
            var dto = await httpClient.GetAsync<Gen.ListAccountAddressesResponseDto>(
                $"public-api/v1/accounts/list-addresses?accountId={Uri.EscapeDataString(accountId)}",
                CreateAuthHeaders(),
                cancellationToken).ConfigureAwait(false);
            return dto?.ToPublic().AccountAddresses ?? Array.Empty<AccountAddressResponse>();
        }

        public async Task<AccountResponse?> UpdateAccountDisplayNameAsync(string accountId, string displayName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new PeakError(PeakErrorCode.InvalidArgument, "Account ID is required");
            }
            var payload = new UpdateAccountDisplayNameRequest { AccountId = accountId, DisplayName = displayName };
            var dto = await httpClient.PostAsync<UpdateAccountDisplayNameRequest, Gen.UpdateAccountDisplayNameResponseDto>(
                "public-api/v1/accounts/update-display-name",
                payload,
                CreateAuthHeaders(),
                cancellationToken).ConfigureAwait(false);
            // The server wraps the account ({ "account": {...} }); map the nested DTO.
            return dto?.Account?.ToPublic();
        }

        internal async Task<GetAddressDetailResponse?> GetAddressDetailAsync(string address, CancellationToken cancellationToken = default)
        {
            var encoded = Uri.EscapeDataString(address);
            var dto = await httpClient.GetAsync<Gen.GetAccountAddressWithAccountAndSourceResponseDto>(
                $"public-api/v1/accounts/get-address-detail?address={encoded}",
                CreateAuthHeaders(),
                cancellationToken).ConfigureAwait(false);
            return dto?.ToPublic();
        }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~AccountServiceTests"`
Expected: PASS (both facts).

- [ ] **Step 5: Commit**

```bash
git add packages/peak-sdk-csharp/src/Services/AccountService.cs packages/peak-sdk-csharp/tests/AccountServiceTests.cs
git commit -m "feat: AccountService consumes generated response DTOs (fixes nested update-display-name)"
```

---

## Task 8: Rewire PrivateKeyService response side

**Files:**
- Modify: `packages/peak-sdk-csharp/src/Services/PrivateKeyService.cs`
- Test: `packages/peak-sdk-csharp/tests/PrivateKeyServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `packages/peak-sdk-csharp/tests/PrivateKeyServiceTests.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KyuzanInc.Peak.Sdk.Models;
using KyuzanInc.Peak.Sdk.Services;
using KyuzanInc.Peak.Sdk.Utils;
using NSubstitute;
using Xunit;
using Gen = KyuzanInc.Peak.PublicApiClient.Model;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class PrivateKeyServiceTests
    {
        [Fact]
        public async Task CompleteImport_MapsGeneratedAccountDtos()
        {
            var http = Substitute.For<IPeakHttpClient>();
            // Build the generated DTO from JSON (parameterless ctor + setters),
            // avoiding the required-arg ctors and their null checks.
            var responseDto = PeakResponseJson.Deserialize<Gen.CompleteImportPrivateKeyResponseDto>(
                "{\"account\":{\"id\":\"acc1\",\"userId\":\"u1\",\"accountSourceId\":\"src1\",\"accountIndex\":0,\"originProjectId\":\"proj1\"}," +
                "\"accountAddress\":{\"id\":\"ad1\",\"accountId\":\"acc1\",\"address\":\"0xabc\",\"chainType\":\"evm\"}," +
                "\"accountSource\":{\"id\":\"s1\",\"userId\":\"u1\",\"originProjectId\":\"proj1\",\"turnkeyResourceId\":\"tk1\",\"sourceType\":\"private-key\",\"creationMethod\":\"imported\"}}")!;
            http.PostAsync<CompleteImportPrivateKeyRequest, Gen.CompleteImportPrivateKeyResponseDto>(
                    "public-api/v1/private-keys/complete-import",
                    Arg.Any<CompleteImportPrivateKeyRequest>(),
                    Arg.Any<IReadOnlyDictionary<string, string>>(),
                    Arg.Any<CancellationToken>())
                .Returns(responseDto);

            // CompleteImportPrivateKeyAsync decodes the session JWT (org/user) and
            // signs the request with a real P-256 key before the mocked HTTP call.
            var keyPair = global::Turnkey.Crypto.GenerateP256KeyPair();
            var svc = new PrivateKeyService(
                "https://api.peak.xyz", "k",
                sessionJwt: FakeJwt.ValidSession(),
                targetPrivateKey: keyPair.PrivateKey,
                httpClient: http);

            var result = await svc.CompleteImportPrivateKeyAsync(encryptedBundle: "bundle", chainType: "evm");

            result.Account!.Id.Should().Be("acc1");
            result.AccountAddress!.Address.Should().Be("0xabc");
            result.AccountSource!.SourceType.Should().Be("private-key");
        }
    }
}
```

Note: `FakeJwt.ValidSession()` (next step) builds a decodable JWT; the private-key flow decodes the session JWT for `turnkeySubOrgId` / `turnkeyUserId` before the HTTP call. `Turnkey.Crypto`/`Turnkey.Http` flow into the test project transitively through the SDK's `KyuzanInc.Turnkey.Sdk` reference.

- [ ] **Step 2: Create the JWT test helper**

Create `packages/peak-sdk-csharp/tests/FakeJwt.cs`:

```csharp
using System;
using System.Text;

namespace KyuzanInc.Peak.Sdk.Tests
{
    // Builds an unsigned JWT whose payload carries every claim
    // SessionJwt.DecodeSessionJwt requires (exp, organization_id, public_key,
    // session_type, user_id — all non-empty, exp non-zero). DecodeSessionJwt does
    // not verify the signature or expiry, so a dummy signature + far-future exp
    // are fine.
    internal static class FakeJwt
    {
        public static string ValidSession(string orgId = "org-1", string userId = "user-1")
        {
            static string B64Url(string s) =>
                Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var header = B64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}");
            var payload = B64Url(
                "{\"exp\":9999999999," +
                $"\"organization_id\":\"{orgId}\"," +
                "\"public_key\":\"pub-key\"," +
                "\"session_type\":\"api\"," +
                $"\"user_id\":\"{userId}\"}}");
            return $"{header}.{payload}.sig";
        }
    }
}
```

- [ ] **Step 3: Run it to verify it fails**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~PrivateKeyServiceTests"`
Expected: FAIL to compile — `CompleteImportPrivateKeyAsync` still calls `PostAsync<..., CompleteImportPrivateKeyResponse>`, so the `Gen.*` mock does not match.

- [ ] **Step 4: Rewire the three response handlers**

In `packages/peak-sdk-csharp/src/Services/PrivateKeyService.cs`, add usings:

```diff
 using KyuzanInc.Peak.Sdk.Models;
 using KyuzanInc.Peak.Sdk.Utils;
+using KyuzanInc.Peak.Sdk.Mapping;
+using Gen = KyuzanInc.Peak.PublicApiClient.Model;
```

In `InitImportPrivateKeyAsync`, change only the response type (the request and result are unchanged):

```csharp
            var initResponse = await httpClient.PostAsync<InitImportPrivateKeyRequest, Gen.InitImportPrivateKeyResponseDto>(
                "public-api/v1/private-keys/init-import",
                new InitImportPrivateKeyRequest { SignedInitImportPrivateKeyRequest = signedInitImportRequest },
                CreateAuthHeaders(),
                cancellationToken).ConfigureAwait(false);
```

In `CompleteImportPrivateKeyAsync`, change the response type and map the account DTOs:

```csharp
            var completeResponse = await httpClient.PostAsync<CompleteImportPrivateKeyRequest, Gen.CompleteImportPrivateKeyResponseDto>(
                "public-api/v1/private-keys/complete-import",
                new CompleteImportPrivateKeyRequest { ChainType = chainType, SignedCompleteImportPrivateKeyRequest = signedCompleteRequest },
                CreateAuthHeaders(),
                cancellationToken).ConfigureAwait(false);

            if (completeResponse == null)
            {
                throw new PeakError(PeakErrorCode.TurnkeyError, "Complete import response was null");
            }

            return new CompleteImportPrivateKeyResult
            {
                Account = completeResponse.Account?.ToPublic(),
                AccountAddress = completeResponse.AccountAddress?.ToPublic(),
                AccountSource = completeResponse.AccountSource?.ToPublic(),
            };
```

In `ExportPrivateKeyAsync`, change only the response type (the `sourceType` branch above already reads the mapped `addressDetail.AccountSource.SourceType` from `AccountService`):

```csharp
            var exportResponse = await httpClient.PostAsync<ExportPrivateKeyRequest, Gen.ExportPrivateKeyResponseDto>(
                "public-api/v1/private-keys/export",
                new ExportPrivateKeyRequest { SourceType = sourceType, SignedExportPrivateKeyRequest = signedExportRequest },
                CreateAuthHeaders(),
                cancellationToken).ConfigureAwait(false);
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~PrivateKeyServiceTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add packages/peak-sdk-csharp/src/Services/PrivateKeyService.cs packages/peak-sdk-csharp/tests/PrivateKeyServiceTests.cs packages/peak-sdk-csharp/tests/FakeJwt.cs
git commit -m "feat: PrivateKeyService consumes generated response DTOs"
```

---

## Task 9: Public-surface baseline test (D13 layer 2)

**Files:**
- Test: `packages/peak-sdk-csharp/tests/PublicSurfaceBaselineTests.cs`
- Create: `packages/peak-sdk-csharp/tests/PublicApi.Sdk.approved.txt`

- [ ] **Step 1: Write the test**

Create `packages/peak-sdk-csharp/tests/PublicSurfaceBaselineTests.cs`:

```csharp
using System.IO;
using System.Runtime.CompilerServices;
using FluentAssertions;
using PublicApiGenerator;
using Xunit;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class PublicSurfaceBaselineTests
    {
        [Fact]
        public void PublicSurface_HasNoGeneratedTypes_AndMatchesBaseline()
        {
            var api = typeof(PeakClient).Assembly
                .GeneratePublicApi()
                .Replace("\r\n", "\n");

            // D13: no generated type may appear on the SDK public surface.
            api.Should().NotContain("KyuzanInc.Peak.PublicApiClient");

            var approvedPath = ApprovedPath();
            var receivedPath = approvedPath.Replace(".approved.", ".received.");

            // Never auto-approve: a missing or empty baseline must FAIL so CI
            // cannot silently accept surface drift. Write the received file for
            // the engineer to inspect and promote (Step 2).
            if (!File.Exists(approvedPath) || new FileInfo(approvedPath).Length == 0)
            {
                File.WriteAllText(receivedPath, api);
                Assert.Fail($"Approved baseline missing/empty. Review '{receivedPath}' and copy it to '{approvedPath}'.");
            }

            var approved = File.ReadAllText(approvedPath).Replace("\r\n", "\n");
            if (api != approved)
            {
                File.WriteAllText(receivedPath, api); // for inspection on drift
            }
            api.Should().Be(approved);
        }

        private static string ApprovedPath([CallerFilePath] string thisFile = "") =>
            Path.Combine(Path.GetDirectoryName(thisFile)!, "PublicApi.Sdk.approved.txt");
    }
}
```

- [ ] **Step 2: Produce and promote the approved baseline**

The committed test FAILS when no approved baseline exists, so CI never auto-approves. Run it once to produce the received output, inspect it, then promote it:

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~PublicSurfaceBaselineTests"`
Expected: FAIL ("Approved baseline missing/empty"); `packages/peak-sdk-csharp/tests/PublicApi.Sdk.received.txt` is written.

Run: `grep -c 'KyuzanInc.Peak.PublicApiClient' packages/peak-sdk-csharp/tests/PublicApi.Sdk.received.txt`
Expected: `0` — no generated type leaked into the surface.

Promote it and ignore stray received files:
Run: `mv packages/peak-sdk-csharp/tests/PublicApi.Sdk.received.txt packages/peak-sdk-csharp/tests/PublicApi.Sdk.approved.txt`
Run: `echo 'packages/peak-sdk-csharp/tests/*.received.txt' >> .gitignore`

- [ ] **Step 3: Verify the baseline holds**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~PublicSurfaceBaselineTests"`
Expected: PASS. Confirm the approved file contains `KyuzanInc.Peak.Sdk.Models.AccountResponse` and does **not** contain `PublicApiClient`.

- [ ] **Step 4: Commit**

```bash
git add packages/peak-sdk-csharp/tests/PublicSurfaceBaselineTests.cs \
  packages/peak-sdk-csharp/tests/PublicApi.Sdk.approved.txt \
  .gitignore
git commit -m "test: lock SDK public surface and assert no generated types leak"
```

---

## Task 10: Docs, follow-ups, and full verification

**Files:**
- Modify: `docs/sync-rules.md`

- [ ] **Step 1: Document the consumer wiring**

Append a short subsection to `docs/sync-rules.md` (under the generated-client section) — adapt to that file's existing headings:

```markdown
### Consumer wiring

`KyuzanInc.Peak.Sdk` consumes the generated client's **response** DTOs: the
transport deserializes them with Newtonsoft and `Mapping/GeneratedDtoMappers.cs`
maps them to the public System.Text.Json DTOs. The generation script flips all
generated types to `internal`; regenerate with `scripts/generate-public-api-client.sh`
(the internalize step runs automatically and the drift job reproduces it).
Requests and the public DTO surface are hand-written and unchanged.
```

- [ ] **Step 2: Run the full SDK test suite**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "Category!=E2E"`
Expected: PASS — all new tests plus the existing `TurnkeyWireFormatSmokeTests`, `PeakClientTests`, `PeakErrorTests`, `SessionJwtTests`, `InMemoryStorageTests` are green. (Requires GitHub Packages auth; otherwise run in CI.)

- [ ] **Step 3: Packaging check (D13 + Newtonsoft dependency)**

Run: `dotnet pack packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj -c Release -o /tmp/peak-nupkg`
Then inspect the produced `.nupkg` (it is a zip):

Run: `unzip -l /tmp/peak-nupkg/KyuzanInc.Peak.Sdk.*.nupkg | grep -E 'KyuzanInc.Peak.PublicApiClient.dll|\.nuspec'`
Expected: `KyuzanInc.Peak.PublicApiClient.dll` appears under each `lib/<tfm>/`.

Run: `unzip -p /tmp/peak-nupkg/KyuzanInc.Peak.Sdk.*.nupkg 'KyuzanInc.Peak.Sdk.nuspec' | grep -iE 'Newtonsoft|ComponentModel.Annotations|PublicApiClient'`
Expected: `<dependency id="Newtonsoft.Json" ...>` and `<dependency id="System.ComponentModel.Annotations" ...>` are present (the latter is needed by the embedded DTOs' `IValidatableObject` on netstandard2.1); **no** dependency on `KyuzanInc.Peak.PublicApiClient`.

- [ ] **Step 4: Track the deferred follow-ups**

Add two rows to the follow-up list in `plans/plans-peak-sdk-csharp.md` (or the repo's tracked follow-up location), matching its table format:

- Unity `link.xml`: preserve `Newtonsoft.Json` and the generated `KyuzanInc.Peak.PublicApiClient.Model` namespace in `peak-sdk-csharp-unity` so IL2CPP stripping keeps them (spec R1/OQ2).
- Slim the generated client to models-only once a generator-core bump can emit dependency-light DTOs, dropping RestSharp/Polly (spec R2/OQ3); also revisit request-DTO adoption.

- [ ] **Step 5: Commit**

```bash
git add docs/sync-rules.md plans/plans-peak-sdk-csharp.md
git commit -m "docs: record consumer wiring and track Unity link.xml + models-only follow-ups"
```

---

## Done-when

- All new tests and the pre-existing suite pass under `--filter "Category!=E2E"` (CI runs the full solution and the `openapi-client-drift` job, which now reproduces the internalize step).
- `GeneratedClientInternalizationTests` and `PublicSurfaceBaselineTests` both pass — no generated type leaks at the assembly or package level.
- The packaging check shows the client DLL embedded and `Newtonsoft.Json` declared, with no dependency on the generated client.
- `TurnkeyWireFormatSmokeTests` is unchanged and green (requests + signed envelopes untouched).
