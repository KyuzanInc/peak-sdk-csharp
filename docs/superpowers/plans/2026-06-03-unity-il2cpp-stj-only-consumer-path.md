# Unity / IL2CPP STJ-only consumer path — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `KyuzanInc.Peak.Sdk` consumable from Unity / IL2CPP by deserializing peak-server responses **directly into the SDK's public System.Text.Json DTOs** (source-gen `PeakJsonContext`), removing Newtonsoft.Json and the embedded RestSharp generated client from the consumer runtime/package path, and pinning `System.Text.Json` to 8.0.x — without changing the public API surface or the Turnkey request wire-format.

**Architecture:** This reverts the response (de)serialization layer that PR #14 added. The three services stop requesting generated `Gen.*ResponseDto` types and `.ToPublic()`-mapping them; they request the public DTOs directly, which the transport deserializes with STJ source-gen. The generated client (`KyuzanInc.Peak.PublicApiClient`) is **kept build-time only** (codegen + `openapi-client-drift` CI + a new field-coverage contract test) but is removed from the SDK's runtime/package reference. Newtonsoft, `System.ComponentModel.Annotations`, the generated→public mappers, and the tolerant-enum resolver are deleted from the SDK.

**Tech Stack:** .NET 8 / netstandard2.1 / net8.0-windows, System.Text.Json 8.0.5 (source-gen `PeakJsonContext`), `KyuzanInc.Turnkey.Sdk` (BouncyCastle + STJ 8.0.5), xUnit + FluentAssertions + NSubstitute, PublicApiGenerator, openapi-generator-cli 7.9.0 (build-time only).

**Spec:** [docs/superpowers/specs/2026-06-03-unity-il2cpp-stj-only-consumer-path-design.md](../specs/2026-06-03-unity-il2cpp-stj-only-consumer-path-design.md) (r4, Codex-READY).

---

## Prerequisites and local constraints (read first)

- **GitHub Packages auth:** the SDK depends on `KyuzanInc.Turnkey.Sdk` from GitHub Packages. A full-solution `dotnet restore` needs a PAT with `read:packages` (see [docs/development.md](../../development.md) / `MEMORY.md`). Local restore/build/test is known to work with that auth in place; without it, restore 401s — push and let CI run the full build/test in that case.
- **Lock-file refresh (Task 5)** uses `dotnet restore peak-sdk-csharp.sln --force-evaluate` to regenerate all three `packages.lock.json` files. An *unauthenticated* restore corrupts lock files — only run it with auth, and probe with `--locked-mode` after.
- **Repo rules:** `Directory.Build.props` sets `TreatWarningsAsErrors=true` and `ImplicitUsings=disable` for product code — every SDK file needs explicit `using`s and must be warning-clean. `dotnet format --verify-no-changes` runs in CI (excluding the generated client) and will fail on an orphaned `using`.
- **Never hand-edit generated code:** `packages/peak-public-api-client-csharp/src/**` is codegen. This plan does **not** touch it. `AssemblyAttributes.cs` lives *outside* `src/`, so editing it (Task 5) is safe from the drift job.
- All paths are relative to the repo root (the worktree checkout).
- **Commit after every task.** Branch is already `feature/ecstatic-lamport-b6693f`.

---

## File structure

**Created:**
- `packages/peak-sdk-csharp/src/Models/UpdateAccountDisplayNameEnvelope.cs` — `internal` STJ wrapper for the `{"account":{...}}` update-display-name response (keeps the public surface unchanged).
- `packages/peak-sdk-csharp/tests/GeneratedDtoContractTests.cs` — P0 `Category=Contract` test: generated-⊆-public field coverage + public-only allowlist + shared-field type-category (spec §6.7).

**Modified:**
- `packages/peak-sdk-csharp/src/Services/AuthService.cs`, `AccountService.cs`, `PrivateKeyService.cs` — response generics → public DTOs; drop `Gen`/`Mapping` usings and `.ToPublic()`.
- `packages/peak-sdk-csharp/src/PeakJsonContext.cs` — register `UpdateAccountDisplayNameEnvelope`.
- `packages/peak-sdk-csharp/src/Serialization/PeakResponseJson.cs` — STJ-only (drop the Newtonsoft branch + `System.Reflection`).
- `packages/peak-sdk-csharp/src/Utils/DefaultPeakHttpClient.cs` — drop the Newtonsoft catch arm + update the XML doc.
- `packages/peak-sdk-csharp/src/Utils/IPeakHttpClient.cs` — drop the Newtonsoft `<remarks>`.
- `packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj` — drop `Newtonsoft.Json`, `System.ComponentModel.Annotations`, the generated-client `ProjectReference`, and the embed target.
- `packages/peak-public-api-client-csharp/AssemblyAttributes.cs` — drop the dead `InternalsVisibleTo("KyuzanInc.Peak.Sdk")`.
- `Directory.Packages.props` — `System.Text.Json` 10.0.8 → 8.0.5; drop `Newtonsoft.Json`, `System.ComponentModel.Annotations`, dead `Microsoft.Extensions.Http.Polly`.
- `packages/peak-sdk-csharp/src/packages.lock.json`, `packages/peak-sdk-csharp/tests/packages.lock.json`, `packages/peak-public-api-client-csharp/packages.lock.json` — refreshed by restore.
- `packages/peak-sdk-csharp/tests/AuthServiceTests.cs`, `AccountServiceTests.cs`, `PrivateKeyServiceTests.cs`, `PeakResponseJsonTests.cs`, `DefaultPeakHttpClientTests.cs` — drive public DTOs through STJ.
- `packages/peak-sdk-csharp/tests/PublicSurfaceBaselineTests.cs` — add a negative assertion for the new internal envelope.
- `.github/workflows/csharp-ci.yml` — banned-package + nuspec assertions (both TFMs) + a net8.0-only runnable STJ smoke.
- `.github/workflows/consumer-smoke.yml` — fix stale comments.
- `docs/architecture.md`, `docs/sync-rules.md`, `README.md`, `plans/plans-peak-sdk-csharp.md` — doc edits (spec §6.11).

**Deleted:**
- `packages/peak-sdk-csharp/src/Mapping/GeneratedDtoMappers.cs`
- `packages/peak-sdk-csharp/src/Serialization/TolerantEnumContractResolver.cs`
- `packages/peak-sdk-csharp/tests/GeneratedDtoMapperTests.cs`

**Kept (explicitly, do NOT delete):**
- `packages/peak-sdk-csharp/tests/GeneratedClientInternalizationTests.cs` — guards the *generated assembly's* exported-types invariant (`PublicSurfaceBaselineTests` only inspects the SDK assembly — spec §6.5).
- `packages/peak-sdk-csharp/src/Serialization/PeakResponseJson.cs` — simplified, not removed (spec OQ1).
- The generated client project, its codegen, the `openapi-client-drift` job, and the test project's `ProjectReference` to the generated client.

---

## Task ordering rationale

Tasks 1–3 retype one service each. This is safe to do incrementally because `PeakResponseJson` keeps its dual STJ/Newtonsoft dispatch until Task 4: a public DTO (not in the generated assembly) routes through the STJ branch, which already resolves it from `PeakJsonContext`. So each service compiles and its tests pass before the transport is cleaned up. Task 4 then removes the now-dead Newtonsoft branch and the mapper/resolver. Task 5 removes the now-unused package/project references. Each task ends green.

---

## Task 1: AuthService → public response DTOs

**Files:**
- Modify: `packages/peak-sdk-csharp/src/Services/AuthService.cs`
- Modify: `packages/peak-sdk-csharp/tests/AuthServiceTests.cs`

- [ ] **Step 1: Rewrite the AuthService tests to drive public DTOs**

Replace the entire contents of `packages/peak-sdk-csharp/tests/AuthServiceTests.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KyuzanInc.Peak.Sdk.Models;
using KyuzanInc.Peak.Sdk.Services;
using KyuzanInc.Peak.Sdk.Utils;
using NSubstitute;
using Xunit;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class AuthServiceTests
    {
        [Fact]
        public async Task InitOtpLogin_ReturnsPublicDto()
        {
            var http = Substitute.For<IPeakHttpClient>();
            http.PostAsync<InitOtpLoginRequest, InitOtpLoginResponse>(
                    "public-api/v1/auth/otp/init-login",
                    Arg.Any<InitOtpLoginRequest>(),
                    Arg.Any<IReadOnlyDictionary<string, string>>(),
                    Arg.Any<CancellationToken>())
                .Returns(new InitOtpLoginResponse { OtpId = "otp-123" });

            var svc = new AuthService("https://api.peak.xyz", "k", http);
            var result = await svc.InitOtpLoginAsync("a@b.c");

            result!.OtpId.Should().Be("otp-123");
        }

        // A custom transport that knows nothing about the SDK's source-gen context
        // and parses reflectively with System.Text.Json still works. Case-insensitive
        // so the camelCase wire body maps onto the PascalCase DTO properties (the
        // SDK's own PeakJsonContext applies a camelCase naming policy instead; plain
        // reflective STJ is case-sensitive by default and would otherwise leave the
        // properties null).
        private sealed class ReflectiveTransport : IPeakHttpClient
        {
            private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
            private readonly string body;
            public ReflectiveTransport(string body) => this.body = body;

            public Task<T?> GetAsync<T>(string endpoint, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default) where T : class =>
                Task.FromResult(JsonSerializer.Deserialize<T>(body, Options));

            public Task<T?> PostAsync<TBody, T>(string endpoint, TBody payload, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default) where TBody : class where T : class =>
                Task.FromResult(JsonSerializer.Deserialize<T>(body, Options));

            public Task<T?> PostAsync<T>(string endpoint, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default) where T : class =>
                Task.FromResult(JsonSerializer.Deserialize<T>(body, Options));
        }

        [Fact]
        public async Task CustomTransport_DeserializesPublicDto_Reflectively()
        {
            var svc = new AuthService("https://api.peak.xyz", "k",
                new ReflectiveTransport("{\"otpId\":\"otp-xyz\"}"));
            var result = await svc.InitOtpLoginAsync("a@b.c");
            result!.OtpId.Should().Be("otp-xyz");
        }

        [Fact]
        public async Task CompleteOtpLogin_MapsResultAndCarriesKeyPair()
        {
            var http = Substitute.For<IPeakHttpClient>();
            http.PostAsync<CompleteOtpLoginRequest, CompleteOtpLoginResponse>(
                    "public-api/v1/auth/otp/complete-login",
                    Arg.Any<CompleteOtpLoginRequest>(),
                    Arg.Any<IReadOnlyDictionary<string, string>>(),
                    Arg.Any<CancellationToken>())
                .Returns(new CompleteOtpLoginResponse
                {
                    User = new UserResponse { Id = "u1", Email = "a@b.c" },
                    SessionJwt = "jwt-x",
                    IsNewUser = true,
                });

            // Default keyPairFactory generates a real P-256 key before the POST.
            var svc = new AuthService("https://api.peak.xyz", "k", http);
            var result = await svc.CompleteOtpLoginAsync("a@b.c", "otp1", "123456");

            result.SessionJwt.Should().Be("jwt-x");
            result.IsNewUser.Should().BeTrue();
            result.User!.Email.Should().Be("a@b.c");
            result.KeyPair!.PrivateKey.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task CompleteOtpLogin_NullResponse_ThrowsAuthenticationFailed()
        {
            // Unconfigured PostAsync -> Task.FromResult(default) -> null DTO.
            var http = Substitute.For<IPeakHttpClient>();
            var svc = new AuthService("https://api.peak.xyz", "k", http);

            Func<Task> act = () => svc.CompleteOtpLoginAsync("a@b.c", "otp1", "123456");

            (await act.Should().ThrowAsync<PeakError>()).Which.Code.Should().Be(PeakErrorCode.AuthenticationFailed);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail (red)**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~AuthServiceTests"`
Expected: the project **compiles** (`IPeakHttpClient` is generic), but the mock-based facts `InitOtpLogin_ReturnsPublicDto` and `CompleteOtpLogin_MapsResultAndCarriesKeyPair` **FAIL (red)**: they configure the substitute for `PostAsync<…, InitOtpLoginResponse>` / `<…, CompleteOtpLoginResponse>` while `AuthService` still calls `PostAsync<…, Gen.*ResponseDto>`, so NSubstitute returns `null` for the service's actual call and the assertions fail. (`CompleteOtpLogin_NullResponse_...` and `CustomTransport_...` don't depend on that mock match and stay green — that's fine; the two red facts are what Step 3 turns green.)

- [ ] **Step 3: Retype AuthService to the public DTOs**

In `packages/peak-sdk-csharp/src/Services/AuthService.cs`, remove these two usings:

```diff
-using KyuzanInc.Peak.Sdk.Mapping;
-using Gen = KyuzanInc.Peak.PublicApiClient.Model;
```

Replace the body of `InitOtpLoginAsync` with:

```csharp
            logger.LogInformation("Starting init OTP login for: {Email}", email);
            var payload = new InitOtpLoginRequest { Email = email };
            var response = await httpClient.PostAsync<InitOtpLoginRequest, InitOtpLoginResponse>(
                "public-api/v1/auth/otp/init-login", payload, cancellationToken: cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Init OTP login successful - OTP ID: {OtpId}", response?.OtpId);
            return response;
```

In `CompleteOtpLoginAsync`, replace the deserialize + map block (the `keyPair` generation above it is unchanged) so it reads:

```csharp
            var response = await httpClient.PostAsync<CompleteOtpLoginRequest, CompleteOtpLoginResponse>(
                "public-api/v1/auth/otp/complete-login", payload, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response == null)
            {
                throw new PeakError(PeakErrorCode.AuthenticationFailed,
                    "Complete OTP login response was empty");
            }

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

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~AuthServiceTests"`
Expected: PASS (all four facts). `PeakResponseJson` still has its dual dispatch, so the rest of the suite stays green.

- [ ] **Step 5: Commit**

```bash
git add packages/peak-sdk-csharp/src/Services/AuthService.cs packages/peak-sdk-csharp/tests/AuthServiceTests.cs
git commit -m "port: AuthService deserializes responses directly into public STJ DTOs"
```

---

## Task 2: AccountService → public DTOs + internal update-display-name envelope

**Files:**
- Create: `packages/peak-sdk-csharp/src/Models/UpdateAccountDisplayNameEnvelope.cs`
- Modify: `packages/peak-sdk-csharp/src/PeakJsonContext.cs`
- Modify: `packages/peak-sdk-csharp/src/Services/AccountService.cs`
- Modify: `packages/peak-sdk-csharp/tests/AccountServiceTests.cs`

- [ ] **Step 1: Create the internal response envelope**

The update-display-name endpoint returns `{"account":{...}}`, and no public type has a lone `Account` field (the public method returns `AccountResponse?`). Add an `internal` wrapper so the public surface is unchanged. The name deliberately avoids the generated `UpdateAccountDisplayNameResponseDto` near-collision.

Create `packages/peak-sdk-csharp/src/Models/UpdateAccountDisplayNameEnvelope.cs`:

```csharp
namespace KyuzanInc.Peak.Sdk.Models
{
    // Internal response wrapper for POST /accounts/update-display-name, which the
    // server shapes as { "account": {...} }. Internal so it does not touch the
    // public surface (the public method returns AccountResponse?). Registered in
    // PeakJsonContext for AOT-safe source-gen deserialization.
    internal sealed class UpdateAccountDisplayNameEnvelope
    {
        public AccountResponse? Account { get; set; }
    }
}
```

- [ ] **Step 2: Register the envelope in PeakJsonContext**

In `packages/peak-sdk-csharp/src/PeakJsonContext.cs`, add the registration in the "Response wrappers" group (after `GetAddressDetailResponse`):

```diff
     [JsonSerializable(typeof(GetAddressDetailResponse))]
+    // Internal wrapper for the { "account": {...} } update-display-name response.
+    [JsonSerializable(typeof(UpdateAccountDisplayNameEnvelope))]
     [JsonSerializable(typeof(InitOtpLoginResponse))]
```

(`PeakJsonContext.cs` already has `using KyuzanInc.Peak.Sdk.Models;`, so the simple type name resolves. STJ source-gen supports an `internal` type in the `internal` context.)

- [ ] **Step 3: Rewrite the AccountService tests to drive public DTOs**

Replace the entire contents of `packages/peak-sdk-csharp/tests/AccountServiceTests.cs` with:

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

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class AccountServiceTests
    {
        [Fact]
        public async Task ListAccounts_ReturnsPublicDtos()
        {
            var http = Substitute.For<IPeakHttpClient>();
            http.GetAsync<ListAccountsResponse>(
                    "public-api/v1/accounts/list", Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(new ListAccountsResponse
                {
                    Accounts = new[] { new AccountResponse { Id = "acc1", AccountIndex = 2 } },
                });

            var svc = new AccountService("https://api.peak.xyz", "k", "jwt", http);
            var accounts = await svc.ListAccountsAsync();

            accounts.Should().ContainSingle();
            accounts[0].Id.Should().Be("acc1");
            accounts[0].AccountIndex.Should().Be(2);
        }

        [Fact]
        public async Task UpdateDisplayName_ReturnsNestedAccount()
        {
            var http = Substitute.For<IPeakHttpClient>();
            http.PostAsync<UpdateAccountDisplayNameRequest, UpdateAccountDisplayNameEnvelope>(
                    "public-api/v1/accounts/update-display-name",
                    Arg.Any<UpdateAccountDisplayNameRequest>(),
                    Arg.Any<IReadOnlyDictionary<string, string>>(),
                    Arg.Any<CancellationToken>())
                .Returns(new UpdateAccountDisplayNameEnvelope
                {
                    Account = new AccountResponse { Id = "acc1", DisplayName = "new name" },
                });

            var svc = new AccountService("https://api.peak.xyz", "k", "jwt", http);
            var account = await svc.UpdateAccountDisplayNameAsync("acc1", "new name");

            account!.Id.Should().Be("acc1");
            account.DisplayName.Should().Be("new name");
        }

        [Fact]
        public async Task ListAccountAddresses_ReturnsPublicDtos()
        {
            var http = Substitute.For<IPeakHttpClient>();
            http.GetAsync<ListAccountAddressesResponse>(
                    Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(new ListAccountAddressesResponse
                {
                    AccountAddresses = new[] { new AccountAddressResponse { Id = "ad1", Address = "0xabc", ChainType = "evm" } },
                });

            var svc = new AccountService("https://api.peak.xyz", "k", "jwt", http);
            var addresses = await svc.ListAccountAddressesAsync("acc1");

            addresses.Should().ContainSingle();
            addresses[0].Address.Should().Be("0xabc");
            addresses[0].ChainType.Should().Be("evm");
        }

        [Fact]
        public async Task ListAccounts_NullResponse_ReturnsEmptyArray()
        {
            // Unconfigured NSubstitute returns Task.FromResult(default) -> null DTO.
            var http = Substitute.For<IPeakHttpClient>();
            var svc = new AccountService("https://api.peak.xyz", "k", "jwt", http);

            var accounts = await svc.ListAccountsAsync();

            accounts.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public async Task ListAccountAddresses_NullResponse_ReturnsEmptyArray()
        {
            var http = Substitute.For<IPeakHttpClient>();
            var svc = new AccountService("https://api.peak.xyz", "k", "jwt", http);

            var addresses = await svc.ListAccountAddressesAsync("acc1");

            addresses.Should().NotBeNull().And.BeEmpty();
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail (red)**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~AccountServiceTests"`
Expected: **compiles**, but the mock-based facts (`ListAccounts_ReturnsPublicDtos`, `UpdateDisplayName_ReturnsNestedAccount`, `ListAccountAddresses_ReturnsPublicDtos`) **FAIL (red)**: they configure the substitute for the public DTOs / `UpdateAccountDisplayNameEnvelope` while the service methods still call `GetAsync<Gen.*>` / `PostAsync<…, Gen.*>`, so the substitute returns `null`. (The two `*_NullResponse_ReturnsEmptyArray` facts stay green either way.)

- [ ] **Step 5: Retype AccountService's four methods**

In `packages/peak-sdk-csharp/src/Services/AccountService.cs`, remove these two usings:

```diff
-using KyuzanInc.Peak.Sdk.Mapping;
-using Gen = KyuzanInc.Peak.PublicApiClient.Model;
```

Replace the four method bodies (`ListAccountsAsync`, `ListAccountAddressesAsync`, `UpdateAccountDisplayNameAsync`, `GetAddressDetailAsync`):

```csharp
        public async Task<AccountResponse[]> ListAccountsAsync(CancellationToken cancellationToken = default)
        {
            var dto = await httpClient.GetAsync<ListAccountsResponse>(
                "public-api/v1/accounts/list", CreateAuthHeaders(), cancellationToken).ConfigureAwait(false);
            return dto?.Accounts ?? Array.Empty<AccountResponse>();
        }

        public async Task<AccountAddressResponse[]> ListAccountAddressesAsync(string accountId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new PeakError(PeakErrorCode.InvalidArgument, "Account ID is required");
            }
            var dto = await httpClient.GetAsync<ListAccountAddressesResponse>(
                $"public-api/v1/accounts/list-addresses?accountId={Uri.EscapeDataString(accountId)}",
                CreateAuthHeaders(),
                cancellationToken).ConfigureAwait(false);
            return dto?.AccountAddresses ?? Array.Empty<AccountAddressResponse>();
        }

        public async Task<AccountResponse?> UpdateAccountDisplayNameAsync(string accountId, string displayName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new PeakError(PeakErrorCode.InvalidArgument, "Account ID is required");
            }
            var payload = new UpdateAccountDisplayNameRequest { AccountId = accountId, DisplayName = displayName };
            var dto = await httpClient.PostAsync<UpdateAccountDisplayNameRequest, UpdateAccountDisplayNameEnvelope>(
                "public-api/v1/accounts/update-display-name",
                payload,
                CreateAuthHeaders(),
                cancellationToken).ConfigureAwait(false);
            // The server wraps the account ({ "account": {...} }); return the nested DTO.
            return dto?.Account;
        }

        internal async Task<GetAddressDetailResponse?> GetAddressDetailAsync(string address, CancellationToken cancellationToken = default)
        {
            var encoded = Uri.EscapeDataString(address);
            return await httpClient.GetAsync<GetAddressDetailResponse>(
                $"public-api/v1/accounts/get-address-detail?address={encoded}",
                CreateAuthHeaders(),
                cancellationToken).ConfigureAwait(false);
        }
```

Note: `GetAddressDetailAsync` keeps its `internal Task<GetAddressDetailResponse?>` signature, so `PrivateKeyService.ExportPrivateKeyAsync` (which reads `addressDetail.AccountSource.SourceType`) is unaffected — only the deserialize target changed.

- [ ] **Step 6: Run the tests + a full SDK build**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~AccountServiceTests"`
Expected: PASS (all five facts).

Run: `dotnet build packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj -c Release`
Expected: `Build succeeded` (PrivateKeyService still uses `Gen.*` + `.ToPublic()`, which still compile — the mapper/resolver are removed in Task 4).

- [ ] **Step 7: Commit**

```bash
git add packages/peak-sdk-csharp/src/Models/UpdateAccountDisplayNameEnvelope.cs \
  packages/peak-sdk-csharp/src/PeakJsonContext.cs \
  packages/peak-sdk-csharp/src/Services/AccountService.cs \
  packages/peak-sdk-csharp/tests/AccountServiceTests.cs
git commit -m "port: AccountService deserializes responses into public STJ DTOs (internal update-display-name envelope)"
```

---

## Task 3: PrivateKeyService → public response DTOs

**Files:**
- Modify: `packages/peak-sdk-csharp/src/Services/PrivateKeyService.cs`
- Modify: `packages/peak-sdk-csharp/tests/PrivateKeyServiceTests.cs`

- [ ] **Step 1: Rewrite the PrivateKeyService tests to drive public DTOs**

Replace the entire contents of `packages/peak-sdk-csharp/tests/PrivateKeyServiceTests.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KyuzanInc.Peak.Sdk.Models;
using KyuzanInc.Peak.Sdk.Services;
using KyuzanInc.Peak.Sdk.Utils;
using NSubstitute;
using Xunit;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class PrivateKeyServiceTests
    {
        [Fact]
        public async Task CompleteImport_ReturnsPublicDtos()
        {
            var http = Substitute.For<IPeakHttpClient>();
            http.PostAsync<CompleteImportPrivateKeyRequest, CompleteImportPrivateKeyResponse>(
                    "public-api/v1/private-keys/complete-import",
                    Arg.Any<CompleteImportPrivateKeyRequest>(),
                    Arg.Any<IReadOnlyDictionary<string, string>>(),
                    Arg.Any<CancellationToken>())
                .Returns(new CompleteImportPrivateKeyResponse
                {
                    Account = new AccountResponse { Id = "acc1" },
                    AccountAddress = new AccountAddressResponse { Address = "0xabc" },
                    AccountSource = new AccountSourceResponse { SourceType = "private-key" },
                });

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

        private static GetAddressDetailResponse PrivateKeySourceDetail(string sourceType) => new()
        {
            AccountAddress = new AccountAddressResponse { Id = "ad1", Address = "0xabc", ChainType = "evm" },
            Account = new AccountResponse { Id = "acc1" },
            AccountSource = new AccountSourceResponse { Id = "s1", TurnkeyResourceId = "tk1", SourceType = sourceType },
        };

        [Fact]
        public async Task Export_PrivateKeySource_MapsBundle()
        {
            var http = Substitute.For<IPeakHttpClient>();
            http.GetAsync<GetAddressDetailResponse>(
                    Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(PrivateKeySourceDetail("private-key"));
            http.PostAsync<ExportPrivateKeyRequest, ExportPrivateKeyResponse>(
                    "public-api/v1/private-keys/export",
                    Arg.Any<ExportPrivateKeyRequest>(),
                    Arg.Any<IReadOnlyDictionary<string, string>>(),
                    Arg.Any<CancellationToken>())
                .Returns(new ExportPrivateKeyResponse { ExportBundle = "bundle-x" });

            var keyPair = global::Turnkey.Crypto.GenerateP256KeyPair();
            var svc = new PrivateKeyService(
                "https://api.peak.xyz", "k",
                sessionJwt: FakeJwt.ValidSession(),
                targetPrivateKey: keyPair.PrivateKey,
                httpClient: http);

            var result = await svc.ExportPrivateKeyAsync(address: "0xabc", targetPublicKey: keyPair.PublicKey);

            result.ExportBundle.Should().Be("bundle-x");
        }

        [Fact]
        public async Task Export_UnsupportedSourceType_ThrowsInvalidResponse()
        {
            // An unknown sourceType now passes through as a RAW STRING (the tolerant
            // enum resolver is gone), so it reaches the default arm of the
            // source-type switch and still surfaces as PeakError InvalidResponse.
            var http = Substitute.For<IPeakHttpClient>();
            http.GetAsync<GetAddressDetailResponse>(
                    Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(PrivateKeySourceDetail("brand-new-source"));

            // A real P-256 key: the service builds the Turnkey client before the
            // source-type branch, so a dummy key would throw earlier.
            var keyPair = global::Turnkey.Crypto.GenerateP256KeyPair();
            var svc = new PrivateKeyService(
                "https://api.peak.xyz", "k",
                sessionJwt: FakeJwt.ValidSession(),
                targetPrivateKey: keyPair.PrivateKey,
                httpClient: http);

            Func<Task> act = () => svc.ExportPrivateKeyAsync(address: "0xabc", targetPublicKey: keyPair.PublicKey);

            (await act.Should().ThrowAsync<PeakError>()).Which.Code.Should().Be(PeakErrorCode.InvalidResponse);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail (red)**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~PrivateKeyServiceTests"`
Expected: **compiles**, but `CompleteImport_ReturnsPublicDtos` and `Export_PrivateKeySource_MapsBundle` **FAIL (red)**: they configure the substitute for the public response DTOs while the service still calls `PostAsync<…, Gen.*>` / `GetAsync<Gen.*>`, so the substitute returns `null` (the import/export then throws or yields no bundle). (`Export_UnsupportedSourceType_ThrowsInvalidResponse` may stay green at this stage — it asserts only the error code, which the null-address-detail path also produces — and turns into the intended raw-string path after Step 3.)

- [ ] **Step 3: Retype PrivateKeyService's three response generics**

In `packages/peak-sdk-csharp/src/Services/PrivateKeyService.cs`, remove these two usings:

```diff
-using KyuzanInc.Peak.Sdk.Mapping;
-using Gen = KyuzanInc.Peak.PublicApiClient.Model;
```

In `InitImportPrivateKeyAsync`, change the response generic and drop the map (the result is already built from `initResponse.ImportBundle`):

```diff
-            var initResponse = await httpClient.PostAsync<InitImportPrivateKeyRequest, Gen.InitImportPrivateKeyResponseDto>(
+            var initResponse = await httpClient.PostAsync<InitImportPrivateKeyRequest, InitImportPrivateKeyResponse>(
                 "public-api/v1/private-keys/init-import",
                 new InitImportPrivateKeyRequest { SignedInitImportPrivateKeyRequest = signedInitImportRequest },
                 CreateAuthHeaders(),
                 cancellationToken).ConfigureAwait(false);
```

In `CompleteImportPrivateKeyAsync`, change the response generic and read the public DTO directly (drop the three `?.ToPublic()` calls):

```diff
-            var completeResponse = await httpClient.PostAsync<CompleteImportPrivateKeyRequest, Gen.CompleteImportPrivateKeyResponseDto>(
+            var completeResponse = await httpClient.PostAsync<CompleteImportPrivateKeyRequest, CompleteImportPrivateKeyResponse>(
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
-                Account = completeResponse.Account?.ToPublic(),
-                AccountAddress = completeResponse.AccountAddress?.ToPublic(),
-                AccountSource = completeResponse.AccountSource?.ToPublic(),
+                Account = completeResponse.Account,
+                AccountAddress = completeResponse.AccountAddress,
+                AccountSource = completeResponse.AccountSource,
             };
```

In `ExportPrivateKeyAsync`, change the response generic (the rest of the method is unchanged):

```diff
-            var exportResponse = await httpClient.PostAsync<ExportPrivateKeyRequest, Gen.ExportPrivateKeyResponseDto>(
+            var exportResponse = await httpClient.PostAsync<ExportPrivateKeyRequest, ExportPrivateKeyResponse>(
                 "public-api/v1/private-keys/export",
                 new ExportPrivateKeyRequest { SourceType = sourceType, SignedExportPrivateKeyRequest = signedExportRequest },
                 CreateAuthHeaders(),
                 cancellationToken).ConfigureAwait(false);
```

(The `addressDetail.AccountSource.SourceType` read, the Turnkey stamps, and the `signedExportRequest` construction are all unchanged — Turnkey wire-format is untouched.)

- [ ] **Step 4: Run the tests + full SDK build**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~PrivateKeyServiceTests"`
Expected: PASS (all three facts).

Run: `dotnet build packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj -c Release`
Expected: `Build succeeded` — no SDK code references `Gen.*` now, but `GeneratedDtoMappers.cs` / `TolerantEnumContractResolver.cs` still exist (deleted in Task 4) and still compile.

- [ ] **Step 5: Commit**

```bash
git add packages/peak-sdk-csharp/src/Services/PrivateKeyService.cs packages/peak-sdk-csharp/tests/PrivateKeyServiceTests.cs
git commit -m "port: PrivateKeyService deserializes responses into public STJ DTOs"
```

---

## Task 4: Transport + response deserializer → STJ-only; delete mappers & resolver

**Files:**
- Modify: `packages/peak-sdk-csharp/src/Serialization/PeakResponseJson.cs`
- Modify: `packages/peak-sdk-csharp/src/Utils/DefaultPeakHttpClient.cs`
- Modify: `packages/peak-sdk-csharp/src/Utils/IPeakHttpClient.cs`
- Delete: `packages/peak-sdk-csharp/src/Mapping/GeneratedDtoMappers.cs`
- Delete: `packages/peak-sdk-csharp/src/Serialization/TolerantEnumContractResolver.cs`
- Modify: `packages/peak-sdk-csharp/tests/PeakResponseJsonTests.cs`
- Modify: `packages/peak-sdk-csharp/tests/DefaultPeakHttpClientTests.cs`
- Delete: `packages/peak-sdk-csharp/tests/GeneratedDtoMapperTests.cs`

- [ ] **Step 1: Rewrite PeakResponseJsonTests for the STJ public-DTO path**

Replace the entire contents of `packages/peak-sdk-csharp/tests/PeakResponseJsonTests.cs` with:

```csharp
using FluentAssertions;
using KyuzanInc.Peak.Sdk.Models;
using Xunit;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class PeakResponseJsonTests
    {
        [Fact]
        public void Deserialize_PublicDto_ReadsCamelCaseFields()
        {
            const string json = "{\"otpId\":\"otp-123\"}";
            var dto = PeakResponseJson.Deserialize<InitOtpLoginResponse>(json);
            dto.Should().NotBeNull();
            dto!.OtpId.Should().Be("otp-123");
        }

        [Fact]
        public void Deserialize_PublicDto_ReadsEnumishStringPassthrough()
        {
            const string json =
                "{\"id\":\"a1\",\"accountId\":\"acc1\",\"address\":\"0xabc\",\"chainType\":\"evm\"}";
            var dto = PeakResponseJson.Deserialize<AccountAddressResponse>(json);
            dto.Should().NotBeNull();
            dto!.ChainType.Should().Be("evm");
        }

        [Fact]
        public void Deserialize_UnregisteredType_ThrowsPeakError()
        {
            // The AOT guarantee: a type not registered in PeakJsonContext throws
            // rather than silently falling back to reflection.
            System.Action act = () => PeakResponseJson.Deserialize<UnregisteredProbe>("{}");
            act.Should().Throw<PeakError>().Which.Code.Should().Be(PeakErrorCode.InvalidArgument);
        }

        private sealed class UnregisteredProbe { public string? X { get; set; } }
    }
}
```

- [ ] **Step 2: Rewrite DefaultPeakHttpClientTests for the STJ path**

Replace the entire contents of `packages/peak-sdk-csharp/tests/DefaultPeakHttpClientTests.cs` with:

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KyuzanInc.Peak.Sdk.Models;
using KyuzanInc.Peak.Sdk.Utils;
using Xunit;

namespace KyuzanInc.Peak.Sdk.Tests
{
    // Covers DefaultPeakHttpClient.SendAsync response branches on the STJ-only
    // path: success deserialization, the catch(JsonException) -> InvalidResponse
    // mapping, the empty-body short-circuit, and the HTTP-error mapping.
    public class DefaultPeakHttpClientTests
    {
        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode status;
            private readonly string body;
            public StubHandler(HttpStatusCode status, string body)
            {
                this.status = status;
                this.body = body;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }

        private static DefaultPeakHttpClient ClientReturning(HttpStatusCode status, string body) =>
            new DefaultPeakHttpClient("https://api.peak.xyz", "test-key", new HttpClient(new StubHandler(status, body)));

        [Fact]
        public async Task GetAsync_PublicDto_DeserializesSuccessBody()
        {
            var client = ClientReturning(HttpStatusCode.OK, "{\"otpId\":\"otp-9\"}");
            var dto = await client.GetAsync<InitOtpLoginResponse>("public-api/v1/auth/otp/init-login");
            dto!.OtpId.Should().Be("otp-9");
        }

        [Fact]
        public async Task GetAsync_MalformedBody_MapsToInvalidResponse()
        {
            var client = ClientReturning(HttpStatusCode.OK, "{ not valid json");
            Func<Task> act = () => client.GetAsync<InitOtpLoginResponse>("public-api/v1/x");
            (await act.Should().ThrowAsync<PeakError>()).Which.Code.Should().Be(PeakErrorCode.InvalidResponse);
        }

        [Fact]
        public async Task GetAsync_NonStringEnumToken_MapsToInvalidResponse()
        {
            // A non-string chainType token now fails closed (STJ rejects 1 into a
            // string? field) instead of being softened to null by the old resolver.
            var client = ClientReturning(HttpStatusCode.OK, "{\"id\":\"a1\",\"chainType\":1}");
            Func<Task> act = () => client.GetAsync<AccountAddressResponse>("public-api/v1/x");
            (await act.Should().ThrowAsync<PeakError>()).Which.Code.Should().Be(PeakErrorCode.InvalidResponse);
        }

        [Fact]
        public async Task GetAsync_UnknownEnumString_PassesThroughRaw()
        {
            // An unknown chainType (e.g. a future "aptos") now passes through as the
            // raw string rather than being nulled by the removed tolerant resolver.
            var client = ClientReturning(HttpStatusCode.OK, "{\"id\":\"a1\",\"chainType\":\"aptos\"}");
            var dto = await client.GetAsync<AccountAddressResponse>("public-api/v1/x");
            dto!.ChainType.Should().Be("aptos");
        }

        [Fact]
        public async Task GetAsync_DecimalAccountIndexToken_MapsToInvalidResponse()
        {
            // accountIndex is int on the public DTO; a decimal-bearing token (1.0 or
            // 1.5) is rejected by STJ. This restores the pre-#14 STJ-into-int
            // contract (spec R8) — #14's generated path accepted 1.0 as 1.
            var oneDotZero = ClientReturning(HttpStatusCode.OK, "{\"id\":\"a\",\"accountIndex\":1.0}");
            (await ((Func<Task>)(() => oneDotZero.GetAsync<AccountResponse>("public-api/v1/x")))
                .Should().ThrowAsync<PeakError>()).Which.Code.Should().Be(PeakErrorCode.InvalidResponse);

            var oneDotFive = ClientReturning(HttpStatusCode.OK, "{\"id\":\"a\",\"accountIndex\":1.5}");
            (await ((Func<Task>)(() => oneDotFive.GetAsync<AccountResponse>("public-api/v1/x")))
                .Should().ThrowAsync<PeakError>()).Which.Code.Should().Be(PeakErrorCode.InvalidResponse);
        }

        [Fact]
        public async Task GetAsync_IntegerAccountIndexToken_Deserializes()
        {
            var client = ClientReturning(HttpStatusCode.OK, "{\"id\":\"a\",\"accountIndex\":2}");
            var dto = await client.GetAsync<AccountResponse>("public-api/v1/x");
            dto!.AccountIndex.Should().Be(2);
        }

        [Fact]
        public async Task GetAsync_MissingRequiredFields_SilentlyDefault()
        {
            // Restored pre-#14 contract (spec §6.3 / R6): the public DTOs are not
            // [JsonRequired], so a response missing fields deserializes with those
            // fields at their defaults rather than throwing. Consumers MUST
            // null/empty-check identity/address fields before use (README response-
            // validation note, Task 8). #14's generated DTOs hard-failed this.
            var client = ClientReturning(HttpStatusCode.OK, "{}");
            var dto = await client.GetAsync<AccountResponse>("public-api/v1/x");
            dto.Should().NotBeNull();
            dto!.Id.Should().BeNull();
            dto.AccountIndex.Should().Be(0);
        }

        [Fact]
        public async Task GetAsync_EmptyBody_ReturnsNull()
        {
            var client = ClientReturning(HttpStatusCode.OK, string.Empty);
            var dto = await client.GetAsync<InitOtpLoginResponse>("public-api/v1/x");
            dto.Should().BeNull();
        }

        [Fact]
        public async Task GetAsync_HttpErrorStatus_MapsToHttpError()
        {
            var client = ClientReturning(HttpStatusCode.InternalServerError, "boom");
            Func<Task> act = () => client.GetAsync<InitOtpLoginResponse>("public-api/v1/x");
            (await act.Should().ThrowAsync<PeakError>()).Which.Code.Should().Be(PeakErrorCode.HttpError);
        }
    }
}
```

- [ ] **Step 3: Baseline the rewritten tests, then drop the now-redundant mapper test**

This task is a refactor (remove the dead generated/Newtonsoft path), not new behaviour, so the rewritten tests are characterization tests — they pass on the **current** dual-dispatch source because a public DTO already routes through `PeakResponseJson`'s STJ branch and the existing combined `catch` also catches `System.Text.Json.JsonException`.

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~PeakResponseJsonTests|FullyQualifiedName~DefaultPeakHttpClientTests"`
Expected: **PASS** — the new facts (`chainType:1 → InvalidResponse`, `chainType:"aptos"` passthrough, `accountIndex:1.0/1.5 → InvalidResponse`, missing-field defaults, unregistered-type throws) already hold under the current source.

Then delete `GeneratedDtoMapperTests` — it is the only remaining test that exercises the generated→public mappers and the Newtonsoft branch that the next steps remove (the new transport + contract tests cover the surviving behaviour):

```bash
git rm packages/peak-sdk-csharp/tests/GeneratedDtoMapperTests.cs
```

Run the suite once more: `dotnet test peak-sdk-csharp.sln -c Release --filter "Category!=E2E"`
Expected: PASS — no remaining test touches the generated branch or the mappers, so the source simplification below cannot be masked by a stale red test.

- [ ] **Step 4: Simplify PeakResponseJson to STJ-only**

Replace the entire contents of `packages/peak-sdk-csharp/src/Serialization/PeakResponseJson.cs` with:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace KyuzanInc.Peak.Sdk
{
    /// <summary>
    /// Deserializes HTTP response bodies via System.Text.Json source generation
    /// (<see cref="PeakJsonContext"/>). AOT- / IL2CPP-safe: a type not registered
    /// in the context throws rather than falling back to reflection.
    /// </summary>
    internal static class PeakResponseJson
    {
        internal static T? Deserialize<T>(string body) where T : class
        {
            var typeInfo = (JsonTypeInfo<T>?)PeakJsonContext.Default.GetTypeInfo(typeof(T))
                ?? throw new PeakError(PeakErrorCode.InvalidArgument,
                    $"Type {typeof(T).Name} is not registered in PeakJsonContext.");
            return JsonSerializer.Deserialize(body, typeInfo);
        }
    }
}
```

(Drops `using System.Reflection;`, the `GeneratedClientAssembly` field, the `GeneratedClientSettings` Newtonsoft settings, and the generated-assembly branch.)

- [ ] **Step 5: Drop the Newtonsoft catch arm + update docs in the transport and interface**

In `packages/peak-sdk-csharp/src/Utils/DefaultPeakHttpClient.cs`, update the class XML doc:

```diff
     /// <summary>
     /// Default <see cref="IPeakHttpClient"/> implementation backed by
     /// <see cref="System.Net.Http.HttpClient"/>. Request bodies are serialised
-    /// with <see cref="PeakJsonContext"/> (AOT / IL2CPP-safe source generation);
-    /// response bodies are deserialised via <c>PeakResponseJson</c>, which uses
-    /// Newtonsoft.Json for the internal generated DTOs (see
-    /// <see cref="IPeakHttpClient"/> remarks).
+    /// with <see cref="PeakJsonContext"/> (AOT / IL2CPP-safe source generation);
+    /// response bodies are deserialised via <c>PeakResponseJson</c> using the same
+    /// source-generated context (no reflection fallback; throws if a type is not
+    /// registered).
     /// </summary>
```

and replace the combined parse-error catch:

```diff
-            // Both serialisers surface a parse failure as a JsonException: the STJ
-            // path for SDK-own types and the Newtonsoft path for generated DTOs
-            // (via PeakResponseJson). Map either to InvalidResponse with the raw body.
-            catch (Exception ex) when (ex is JsonException or Newtonsoft.Json.JsonException)
+            // A parse failure surfaces as System.Text.Json.JsonException (the only
+            // serialiser on the response path). Map it to InvalidResponse with the
+            // raw body.
+            catch (JsonException ex)
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
```

(The empty-body short-circuit `if (string.IsNullOrEmpty(body)) return null;` and all other catches stay. `JsonException`, `JsonSerializer`, and `JsonTypeInfo` are still used by `PostAsync` request serialization, so no `using` becomes orphaned.)

In `packages/peak-sdk-csharp/src/Utils/IPeakHttpClient.cs`, delete the now-false `<remarks>` block:

```diff
     /// or platform-specific retry policies.
     /// </summary>
-    /// <remarks>
-    /// For endpoints whose response type comes from the internal
-    /// <c>KyuzanInc.Peak.PublicApiClient</c> assembly, a custom implementation
-    /// must deserialize the body with Newtonsoft.Json (for example
-    /// <c>JsonConvert.DeserializeObject(body, typeof(T))</c>) because those types
-    /// are Newtonsoft-shaped. The default <see cref="DefaultPeakHttpClient"/>
-    /// does this automatically.
-    /// </remarks>
     public interface IPeakHttpClient
```

- [ ] **Step 6: Delete the now-unreferenced mappers and resolver**

After Step 4, `PeakResponseJson` no longer references `TolerantEnumContractResolver` (the `GeneratedClientSettings` field is gone), and no SDK source or test references `GeneratedDtoMappers` (`GeneratedDtoMapperTests` was removed in Step 3). Both are now dead — delete them:

```bash
git rm packages/peak-sdk-csharp/src/Mapping/GeneratedDtoMappers.cs \
       packages/peak-sdk-csharp/src/Serialization/TolerantEnumContractResolver.cs
```

- [ ] **Step 7: Build (warnings-as-errors + format) and run the full non-E2E suite**

Run: `dotnet build peak-sdk-csharp.sln -c Release`
Expected: `Build succeeded` with no warnings (an orphaned `using` would fail here under `TreatWarningsAsErrors`).

Run: `dotnet format peak-sdk-csharp.sln --verify-no-changes --no-restore --exclude packages/peak-public-api-client-csharp/`
Expected: no formatting changes.

Run: `dotnet test peak-sdk-csharp.sln -c Release --filter "Category!=E2E"`
Expected: PASS. `TurnkeyWireFormatSmokeTests`, `PublicSurfaceBaselineTests`, `GeneratedClientInternalizationTests`, `PeakClientTests`, `SessionJwtTests`, `InMemoryStorageTests`, `PeakErrorTests` stay green; the reworked service/transport/response-json tests pass.

- [ ] **Step 8: Commit**

```bash
git add packages/peak-sdk-csharp/src/Serialization/PeakResponseJson.cs \
  packages/peak-sdk-csharp/src/Utils/DefaultPeakHttpClient.cs \
  packages/peak-sdk-csharp/src/Utils/IPeakHttpClient.cs \
  packages/peak-sdk-csharp/tests/PeakResponseJsonTests.cs \
  packages/peak-sdk-csharp/tests/DefaultPeakHttpClientTests.cs
git commit -m "port: STJ-only response deserialization; drop Newtonsoft path, mappers, tolerant resolver"
```

---

## Task 5: Remove Newtonsoft / generated-client / Annotations from the package; pin STJ 8.0.5

**Files:**
- Modify: `packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj`
- Modify: `packages/peak-public-api-client-csharp/AssemblyAttributes.cs`
- Modify: `Directory.Packages.props`
- Regenerated by restore: `packages/peak-sdk-csharp/src/packages.lock.json`, `packages/peak-sdk-csharp/tests/packages.lock.json`, `packages/peak-public-api-client-csharp/packages.lock.json`

- [ ] **Step 1: Strip the SDK csproj down to the STJ-only closure**

In `packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj`, remove the `Newtonsoft.Json` + `System.ComponentModel.Annotations` references:

```diff
   <ItemGroup>
     <PackageReference Include="KyuzanInc.Turnkey.Sdk" />
     <PackageReference Include="System.Text.Json" />
     <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
     <PackageReference Include="Microsoft.Extensions.Http" />
-    <PackageReference Include="Newtonsoft.Json" />
-    <!-- Required at runtime by the embedded generated DTOs (IValidatableObject)
-         for netstandard2.1 consumers; the client's own reference is hidden by
-         PrivateAssets=all below. -->
-    <PackageReference Include="System.ComponentModel.Annotations" />
   </ItemGroup>
```

and remove the whole generated-client reference block (the comment, the `ProjectReference` ItemGroup, the `TargetsForTfmSpecificBuildOutput` PropertyGroup, and the `_IncludeGeneratedClientDll` target):

```diff
-  <!--
-    Internal generated DTO client (D13). PrivateAssets=all keeps it off the
-    consumer's package dependency graph; the DLL is embedded into the nupkg by
-    the target below so it loads at runtime (a ProjectReference to an
-    IsPackable=false project is otherwise omitted from the package). The client's
-    types are internal, so the lib/ DLL exposes no public API to reference.
-  -->
-  <ItemGroup>
-    <ProjectReference Include="..\..\peak-public-api-client-csharp\KyuzanInc.Peak.PublicApiClient.csproj"
-                      PrivateAssets="all" />
-  </ItemGroup>
-  <PropertyGroup>
-    <TargetsForTfmSpecificBuildOutput>$(TargetsForTfmSpecificBuildOutput);_IncludeGeneratedClientDll</TargetsForTfmSpecificBuildOutput>
-  </PropertyGroup>
-  <Target Name="_IncludeGeneratedClientDll">
-    <ItemGroup>
-      <BuildOutputInPackage Include="$(OutputPath)KyuzanInc.Peak.PublicApiClient.dll" />
-    </ItemGroup>
-  </Target>
```

Leave the `net8.0-windows` DPAPI reference, the README `None` item, and `<InternalsVisibleTo Include="KyuzanInc.Peak.Sdk.Tests" />` untouched (tests still need SDK-internal access for `PeakResponseJson`, the envelope, and `PeakJsonContext`).

- [ ] **Step 2: Remove the dead SDK friend grant from the generated client**

The SDK no longer references the generated client, so its `InternalsVisibleTo("KyuzanInc.Peak.Sdk")` is dead. In `packages/peak-public-api-client-csharp/AssemblyAttributes.cs`, delete that one line (keep the `.Tests` grant — the contract + internalization tests need it):

```diff
 [assembly: InternalsVisibleTo("KyuzanInc.Peak.Sdk")]
 [assembly: InternalsVisibleTo("KyuzanInc.Peak.Sdk.Tests")]
```
becomes:
```csharp
[assembly: InternalsVisibleTo("KyuzanInc.Peak.Sdk.Tests")]
```

- [ ] **Step 3: Pin STJ to 8.0.5 and drop the dead central versions**

In `Directory.Packages.props`:

```diff
     <!-- JSON: source generator for AOT / IL2CPP safety -->
-    <PackageVersion Include="System.Text.Json" Version="10.0.8" />
+    <!-- JSON: source generator for AOT / IL2CPP safety. Pinned 8.0.x to align
+         with KyuzanInc.Turnkey.Sdk (IL2CPP-proven) and minimize Unity restore risk. -->
+    <PackageVersion Include="System.Text.Json" Version="8.0.5" />
```

Remove the dead `Microsoft.Extensions.Http.Polly` central version (referenced by no project — confirm in Step 4):

```diff
-    <!-- HTTP resilience pattern (Polly v7-line for netstandard2.1 compat) -->
-    <PackageVersion Include="Microsoft.Extensions.Http.Polly" Version="7.0.20" />
     <PackageVersion Include="Microsoft.Extensions.Http" Version="8.0.1" />
```

Remove the `Newtonsoft.Json` + `System.ComponentModel.Annotations` central versions and their comment:

```diff
-    <!-- Internal response-DTO deserialization in the SDK (the generated client's
-         models are Newtonsoft-shaped). The client csproj keeps its own
-         VersionOverride for RestSharp/Polly; Newtonsoft is shared here. -->
-    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
-    <!-- The embedded generated DTOs implement IValidatableObject from
-         System.ComponentModel.DataAnnotations; netstandard2.1 consumers need this
-         assembly to load them, and PrivateAssets hides the client's own copy. -->
-    <PackageVersion Include="System.ComponentModel.Annotations" Version="5.0.0" />
```

Leave the bottom comment block about the generated client's `VersionOverride` (RestSharp/Polly/Newtonsoft) intact — the generated client still pins those for its own build/test use.

- [ ] **Step 4: Confirm nothing else references the dropped packages, then refresh all three lock files**

Run: `grep -rn "Newtonsoft\|Http.Polly\|ComponentModel.Annotations" --include=*.csproj packages/ ; echo "---" ; grep -rn "Http.Polly" packages/ Directory.Packages.props`
Expected: the only remaining `Newtonsoft` / `RestSharp` / `Polly` references are inside `packages/peak-public-api-client-csharp/KyuzanInc.Peak.PublicApiClient.csproj` (its `VersionOverride`s) — none in the SDK csproj, and no `Http.Polly` reference anywhere outside the (now-removed) central entry.

Run (needs GitHub Packages auth): `dotnet restore peak-sdk-csharp.sln --force-evaluate`
Expected: restore succeeds and rewrites all three `packages.lock.json` files (SDK src, tests, generated client — the generated client's lock has a central `System.Text.Json` entry that the 10.0.8→8.0.5 change invalidates).

Then probe locked-mode: `dotnet restore peak-sdk-csharp.sln --locked-mode`
Expected: succeeds (lock files are consistent). If it 401s locally, the auth is missing — push and let CI verify instead.

- [ ] **Step 5: Build, pack, and prove the nupkg is STJ-only**

Run: `dotnet build peak-sdk-csharp.sln -c Release`
Expected: `Build succeeded`.

Run: `dotnet pack packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj -c Release -o /tmp/peak-pack`
Then inspect the package contents and nuspec:

```bash
nupkg=$(ls /tmp/peak-pack/KyuzanInc.Peak.Sdk.*.nupkg | head -1)
unzip -l "$nupkg" | grep -i '\.dll'                 # expect only KyuzanInc.Peak.Sdk.dll under lib/
unzip -p "$nupkg" '*.nuspec' | grep -iE 'Newtonsoft|RestSharp|Polly|ComponentModel.Annotations|System.Text.Json'
```
Expected: the package ships **only** `KyuzanInc.Peak.Sdk.dll` (no `KyuzanInc.Peak.PublicApiClient.dll`); the nuspec `<dependency>` list contains `System.Text.Json` at `8.0.5` and **no** `Newtonsoft.Json`, `RestSharp`, `Polly`, or `System.ComponentModel.Annotations`.

- [ ] **Step 6: Run the full non-E2E suite**

Run: `dotnet test peak-sdk-csharp.sln -c Release --filter "Category!=E2E"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj \
  packages/peak-public-api-client-csharp/AssemblyAttributes.cs \
  Directory.Packages.props \
  packages/peak-sdk-csharp/src/packages.lock.json \
  packages/peak-sdk-csharp/tests/packages.lock.json \
  packages/peak-public-api-client-csharp/packages.lock.json
git commit -m "feat: drop Newtonsoft/RestSharp/Annotations from the package; pin System.Text.Json 8.0.5"
```

---

## Task 6: P0 contract test + public-surface negative assertion

**Files:**
- Create: `packages/peak-sdk-csharp/tests/GeneratedDtoContractTests.cs`
- Modify: `packages/peak-sdk-csharp/tests/PublicSurfaceBaselineTests.cs`

- [ ] **Step 1: Write the contract test**

This is a guard (characterization) test for an invariant that should already hold: the public DTOs must model every generated wire field. If it FAILS on first run, that is a real coverage gap — fix the public DTO (or, for a deliberate public-only field, extend `PublicOnlyAllowlist`); do **not** weaken the assertion.

Create `packages/peak-sdk-csharp/tests/GeneratedDtoContractTests.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Serialization.Metadata;
using FluentAssertions;
using KyuzanInc.Peak.Sdk;
using KyuzanInc.Peak.Sdk.Models;
using Xunit;
using Gen = KyuzanInc.Peak.PublicApiClient.Model;

namespace KyuzanInc.Peak.Sdk.Tests
{
    // P0 contract test (spec §6.7), the only guard for generated-client -> hand-DTO
    // drift now that #14's compile-time binding is gone. For each adopted endpoint:
    //   1. coverage: every generated wire field is modelled by the public DTO,
    //   2. no silent public-only additions beyond an explicit allowlist,
    //   3. shared fields share a JSON type-category.
    // It does NOT assert required-ness (spec §6.3 intentionally relaxes that).
    [Trait("Category", "Contract")]
    public class GeneratedDtoContractTests
    {
        // (public DTO, generated DTO) pairs. The internal update-display-name
        // envelope pairs with the generated UpdateAccountDisplayNameResponseDto.
        private static readonly (Type Public, Type Generated)[] Pairs =
        {
            (typeof(UserResponse), typeof(Gen.UserResponseDto)),
            (typeof(AccountResponse), typeof(Gen.AccountResponseDto)),
            (typeof(AccountAddressResponse), typeof(Gen.AccountAddressResponseDto)),
            (typeof(AccountSourceResponse), typeof(Gen.AccountSourceResponseDto)),
            (typeof(InitOtpLoginResponse), typeof(Gen.InitOtpLoginResponseDto)),
            (typeof(CompleteOtpLoginResponse), typeof(Gen.CompleteOtpLoginResponseDto)),
            (typeof(ListAccountsResponse), typeof(Gen.ListAccountsResponseDto)),
            (typeof(ListAccountAddressesResponse), typeof(Gen.ListAccountAddressesResponseDto)),
            (typeof(GetAddressDetailResponse), typeof(Gen.GetAccountAddressWithAccountAndSourceResponseDto)),
            (typeof(InitImportPrivateKeyResponse), typeof(Gen.InitImportPrivateKeyResponseDto)),
            (typeof(CompleteImportPrivateKeyResponse), typeof(Gen.CompleteImportPrivateKeyResponseDto)),
            (typeof(ExportPrivateKeyResponse), typeof(Gen.ExportPrivateKeyResponseDto)),
            (typeof(UpdateAccountDisplayNameEnvelope), typeof(Gen.UpdateAccountDisplayNameResponseDto)),
        };

        // Legacy public-only fields with no server-spec source (spec §6.7). Adding
        // an entry here must be a deliberate decision, surfaced in review.
        private static readonly Dictionary<Type, HashSet<string>> PublicOnlyAllowlist = new()
        {
            [typeof(UserResponse)] = new HashSet<string>(StringComparer.Ordinal) { "isAuthenticated" },
        };

        [Fact]
        public void PublicDtos_CoverEveryGeneratedWireField()
        {
            var failures = new List<string>();

            foreach (var (pub, gen) in Pairs)
            {
                var pubFields = PublicWireFields(pub);
                var genFields = GeneratedWireFields(gen);
                var allow = PublicOnlyAllowlist.TryGetValue(pub, out var a)
                    ? a : new HashSet<string>(StringComparer.Ordinal);

                foreach (var g in genFields.Keys)
                {
                    if (!pubFields.ContainsKey(g))
                    {
                        failures.Add($"{pub.Name}: generated field '{g}' (on {gen.Name}) is NOT modelled by the public DTO");
                    }
                }

                foreach (var p in pubFields.Keys)
                {
                    if (!genFields.ContainsKey(p) && !allow.Contains(p))
                    {
                        failures.Add($"{pub.Name}: public-only field '{p}' is absent from {gen.Name} and not allow-listed");
                    }

                    if (genFields.TryGetValue(p, out var gcat) && gcat != pubFields[p])
                    {
                        failures.Add($"{pub.Name}.{p}: type-category mismatch (public={pubFields[p]}, generated={gcat})");
                    }
                }
            }

            failures.Should().BeEmpty(
                "the public DTOs must cover every generated wire field (spec §6.7). " +
                "Fix the public DTO, or for a deliberate public-only field extend PublicOnlyAllowlist:\n  " +
                string.Join("\n  ", failures));
        }

        // Public wire names/categories come straight from the SDK's STJ source-gen
        // context, so they are exactly what ships on the wire (camelCase policy).
        private static Dictionary<string, string> PublicWireFields(Type t)
        {
            var info = PeakJsonContext.Default.GetTypeInfo(t)
                ?? throw new InvalidOperationException($"{t.Name} is not registered in PeakJsonContext");
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonPropertyInfo p in info.Properties)
            {
                result[p.Name] = Category(p.PropertyType);
            }
            return result;
        }

        // Generated wire names/categories from the Newtonsoft [DataMember] metadata.
        private static Dictionary<string, string> GeneratedWireFields(Type t)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var dm = prop.GetCustomAttribute<DataMemberAttribute>();
                if (dm is null) continue; // only [DataMember] wire fields
                var name = !string.IsNullOrEmpty(dm.Name) ? dm.Name! : CamelCase(prop.Name);
                result[name] = Category(prop.PropertyType);
            }
            return result;
        }

        private static string Category(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            if (t == typeof(string)) return "string";
            if (t == typeof(bool)) return "bool";
            if (t.IsEnum) return "string"; // generated string enums serialise as strings
            if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
                || t == typeof(decimal) || t == typeof(double) || t == typeof(float))
                return "number";
            if (t != typeof(string) && typeof(IEnumerable).IsAssignableFrom(t)) return "array";
            return "object";
        }

        private static string CamelCase(string s) =>
            string.IsNullOrEmpty(s) || char.IsLower(s[0]) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);
    }
}
```

- [ ] **Step 2: Run the contract test**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~GeneratedDtoContractTests"`
Expected: PASS. If it FAILS with a "generated field ... NOT modelled" message, that is real drift between the pinned spec and the public DTOs — add the missing field to `Models.cs` and its `PeakJsonContext` registration if needed; do not delete the assertion. If it fails with a "public-only field ... not allow-listed" message for a field that genuinely has no server source, add it to `PublicOnlyAllowlist` with a comment.

- [ ] **Step 3: Add the envelope negative assertion to the public-surface test**

In `packages/peak-sdk-csharp/tests/PublicSurfaceBaselineTests.cs`, add an assertion that the new internal wrapper did not leak onto the public surface:

```diff
             api.Should().Contain("class AccountResponse");
             api.Should().Contain("interface IPeakHttpClient");
+
+            // The internal update-display-name wrapper must NOT be public.
+            api.Should().NotContain("UpdateAccountDisplayNameEnvelope");
```

- [ ] **Step 4: Run the public-surface test**

Run: `dotnet test packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj -c Release --filter "FullyQualifiedName~PublicSurfaceBaselineTests"`
Expected: PASS (`NotContain("PublicApiClient")` is now trivially true since the SDK no longer references the generated client; the envelope is internal so `NotContain("UpdateAccountDisplayNameEnvelope")` holds).

- [ ] **Step 5: Commit**

```bash
git add packages/peak-sdk-csharp/tests/GeneratedDtoContractTests.cs packages/peak-sdk-csharp/tests/PublicSurfaceBaselineTests.cs
git commit -m "test: P0 generated-vs-public DTO field-coverage contract test + envelope surface guard"
```

---

## Task 7: CI — consumer dependency-graph + nuspec checks (both TFMs) + net8.0 STJ smoke

**Files:**
- Modify: `.github/workflows/csharp-ci.yml`
- Modify: `.github/workflows/consumer-smoke.yml`

- [ ] **Step 1: Add the banned-package + nuspec assertions to `consumer-restore-check`**

In `.github/workflows/csharp-ci.yml`, the `consumer-restore-check` job's last step is "Smoke consumer project that references the .nupkg" (which restores + builds `consumer.csproj` for the matrix TFM). After that step, add a new step that runs on both TFMs:

```yaml
      - name: Assert STJ-only dependency graph + nuspec (Unity/IL2CPP guard)
        shell: bash
        run: |
          set -euo pipefail
          cd /tmp/consumer
          # 1. No RestSharp/Polly/Newtonsoft anywhere in the consumer's transitive
          #    graph. (Do NOT ban System.ComponentModel.Annotations: it is a benign
          #    transitive of Microsoft.Extensions.Http -> Microsoft.Extensions.Options
          #    on netstandard2.1, not a remnant of the old generated/Newtonsoft path.)
          graph="$(dotnet list consumer.csproj package --include-transitive)"
          for banned in RestSharp Polly Newtonsoft.Json; do
            if grep -qi "$banned" <<<"$graph"; then
              echo "::error::$banned leaked into the consumer graph (Unity/IL2CPP regression)"; exit 1
            fi
          done
          # 2. The packed nuspec must declare System.Text.Json 8.0.x and neither
          #    Newtonsoft.Json nor System.ComponentModel.Annotations.
          nupkg="$(ls "${GITHUB_WORKSPACE}"/local-feed/KyuzanInc.Peak.Sdk.*.nupkg | head -1)"
          nuspec="$(unzip -p "$nupkg" '*.nuspec')"
          if grep -qiE 'id="(Newtonsoft\.Json|System\.ComponentModel\.Annotations|RestSharp|Polly)"' <<<"$nuspec"; then
            echo "::error::nuspec declares a banned dependency"; echo "$nuspec"; exit 1
          fi
          if ! grep -qiE 'id="System\.Text\.Json" version="\[?8\.0\.' <<<"$nuspec"; then
            echo "::error::nuspec does not pin System.Text.Json 8.0.x"; echo "$nuspec"; exit 1
          fi
          echo "STJ-only dependency graph + nuspec verified for ${{ matrix.tfm }}."
```

- [ ] **Step 2: Add the net8.0-only runnable STJ-resolution smoke**

`netstandard2.1` cannot run, so the executed smoke is net8.0 only. Add this step to `consumer-restore-check` after Step 1's step, gated to net8.0:

```yaml
      - name: STJ-resolution execution smoke (net8.0 only)
        if: matrix.tfm == 'net8.0'
        shell: bash
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          set -euo pipefail
          mkdir -p /tmp/stj-smoke && cd /tmp/stj-smoke
          cat > smoke.csproj <<EOF
          <Project Sdk="Microsoft.NET.Sdk">
            <PropertyGroup>
              <OutputType>Exe</OutputType>
              <TargetFramework>net8.0</TargetFramework>
              <Nullable>enable</Nullable>
            </PropertyGroup>
            <ItemGroup>
              <PackageReference Include="KyuzanInc.Peak.Sdk" Version="0.1.0-alpha.0" />
            </ItemGroup>
          </Project>
          EOF
          cat > Program.cs <<'EOF'
          using System;
          using System.Net;
          using System.Net.Http;
          using System.Threading;
          using System.Threading.Tasks;
          using KyuzanInc.Peak.Sdk.Models;
          using KyuzanInc.Peak.Sdk.Utils;

          // A stub handler returns a canned 200 body so the SDK's public GetAsync<T>
          // exercises the real System.Text.Json source-gen path end to end. A missing
          // [JsonSerializable] registration throws here (the actual Unity failure).
          internal sealed class StubHandler : HttpMessageHandler
          {
              protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) =>
                  Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                  { Content = new StringContent("{\"otpId\":\"otp-smoke\"}") });
          }

          internal static class Program
          {
              private static int Main()
              {
                  var client = new DefaultPeakHttpClient(
                      "https://example.invalid/", "key", new HttpClient(new StubHandler()));
                  var dto = client.GetAsync<InitOtpLoginResponse>("public-api/v1/auth/otp/init-login")
                      .GetAwaiter().GetResult();
                  if (dto?.OtpId != "otp-smoke")
                  {
                      Console.Error.WriteLine($"STJ smoke failed: otpId='{dto?.OtpId}'");
                      return 2;
                  }
                  Console.WriteLine("STJ source-gen resolution smoke ok (InitOtpLoginResponse).");
                  return 0;
              }
          }
          EOF
          # Same source layout as the build smoke: local feed for KyuzanInc.Peak.*,
          # GitHub Packages for the transitive KyuzanInc.Turnkey.*, nuget.org for the rest.
          cat > nuget.config <<EOF
          <?xml version="1.0" encoding="utf-8"?>
          <configuration>
            <packageSources>
              <clear />
              <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              <add key="github-kyuzan" value="https://nuget.pkg.github.com/KyuzanInc/index.json" />
              <add key="local" value="${GITHUB_WORKSPACE}/local-feed" />
            </packageSources>
            <packageSourceMapping>
              <packageSource key="nuget.org"><package pattern="*" /></packageSource>
              <packageSource key="github-kyuzan"><package pattern="KyuzanInc.Turnkey.*" /></packageSource>
              <packageSource key="local"><package pattern="KyuzanInc.Peak.*" /></packageSource>
            </packageSourceMapping>
            <packageSourceCredentials>
              <github-kyuzan>
                <add key="Username" value="${{ github.actor }}" />
                <add key="ClearTextPassword" value="${GITHUB_TOKEN}" />
              </github-kyuzan>
            </packageSourceCredentials>
          </configuration>
          EOF
          dotnet run -c Release
```

- [ ] **Step 3: Fix the stale comments in `consumer-smoke.yml`**

In `.github/workflows/consumer-smoke.yml`, the header comment (lines ~11-13) and the source-config comment (lines ~121-123) describe a now-removed embedded DLL and Newtonsoft/Annotations dependency edges. Update them to reflect the STJ-only package.

Replace the header bullet:

```diff
-# catches packaging issues that only surface after publish — a missing
-# embedded DLL, a wrong/absent dependency edge (Newtonsoft.Json,
-# System.ComponentModel.Annotations), or a broken restore from the real feed.
+# catches packaging issues that only surface after publish — a wrong/absent
+# dependency edge (e.g. System.Text.Json) or a broken restore from the real feed.
+# (The SDK is System.Text.Json-only: no embedded generated-client DLL, no
+# Newtonsoft.Json/RestSharp on the consumer path — see issue #18.)
```

Replace the source-config comment:

```diff
-          # Add the org GitHub Packages feed to a LOCAL NuGet.config alongside the
-          # default nuget.org. KyuzanInc.Peak.Sdk AND its transitive
-          # KyuzanInc.Turnkey.Sdk resolve from here; everything else
-          # (Newtonsoft.Json, System.ComponentModel.Annotations, ...) from
-          # nuget.org. The clear-text token is fine on an ephemeral runner — the
-          # job-scoped GITHUB_TOKEN is revoked at job end.
+          # Add the org GitHub Packages feed to a LOCAL NuGet.config alongside the
+          # default nuget.org. KyuzanInc.Peak.Sdk AND its transitive
+          # KyuzanInc.Turnkey.Sdk resolve from here; everything else
+          # (System.Text.Json, Microsoft.Extensions.*, ...) from nuget.org. The
+          # clear-text token is fine on an ephemeral runner — the job-scoped
+          # GITHUB_TOKEN is revoked at job end.
```

- [ ] **Step 4: Validate the workflow YAML**

Run: `python3 -c "import yaml,sys; [yaml.safe_load(open(f)) for f in ['.github/workflows/csharp-ci.yml','.github/workflows/consumer-smoke.yml']]; print('YAML OK')"`
Expected: `YAML OK` (no parse error). The runtime behavior is verified by CI on the PR.

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/csharp-ci.yml .github/workflows/consumer-smoke.yml
git commit -m "ci: assert STJ-only consumer graph + nuspec and add a net8.0 STJ-resolution smoke"
```

---

## Task 8: Documentation

**Files:**
- Modify: `docs/architecture.md`
- Modify: `docs/sync-rules.md`
- Modify: `README.md`
- Modify: `plans/plans-peak-sdk-csharp.md`

- [ ] **Step 1: `docs/architecture.md` — STJ version + generated-client framing**

Edit the dependency-stack box version line (preserve the `|` right-edge alignment by keeping the line length; `8.0.5` is one char shorter than `10.0.8`, so add one space before `(source-gen`):

```diff
-|  System.Text.Json 10.0.8           (source-gen context)  |
+|  System.Text.Json 8.0.5            (source-gen context)  |
```

Edit the `KyuzanInc.Peak.PublicApiClient` box line:

```diff
-|  - referenced as <IsPrivateAssets="all">                 |
+|  - build-time only; not a runtime/package dependency     |
```

Edit the "OpenAPI codegen" prose paragraph (currently "The hand-designed DTOs ... wrap the generated types ..."):

```diff
-`KyuzanInc.Peak.PublicApiClient` is an OpenAPI-generated client. It is
-deliberately not exposed to consumers (`<IsPrivateAssets="all"/>` on
-its `<PackageReference>`). The hand-designed DTOs in
-`KyuzanInc.Peak.Sdk` wrap the generated types so that a codegen
-template change does not break the public surface.
+`KyuzanInc.Peak.PublicApiClient` is an OpenAPI-generated client. It is
+**build-time only**: the SDK does not reference it at runtime and does
+not ship it in the `KyuzanInc.Peak.Sdk` package. It exists to detect
+spec drift (the `openapi-client-drift` CI job regenerates and diffs it)
+and to back the `GeneratedDtoContractTests` field-coverage check. The
+SDK's public DTOs are hand-written System.Text.Json types
+(`Models/Models.cs`) deserialized directly via `PeakJsonContext`; they
+do not wrap the generated types (reverted per issue #18).
```

- [ ] **Step 2: `docs/sync-rules.md` — Consumer wiring section**

Replace the "Consumer wiring" paragraph:

```diff
 ### Consumer wiring

-`KyuzanInc.Peak.Sdk` consumes the generated client's **response** DTOs: the
-transport deserializes them with Newtonsoft and `Mapping/GeneratedDtoMappers.cs`
-maps them to the public System.Text.Json DTOs. The generation script flips all
-generated types to `internal`; regenerate with `scripts/generate-public-api-client.sh`
-(the internalize step runs automatically and the drift job reproduces it).
-Requests and the public DTO surface are hand-written and unchanged.
+`KyuzanInc.Peak.Sdk` deserializes peak-server **responses** directly into its
+hand-written System.Text.Json DTOs (`Models/Models.cs`) via the source-generated
+`PeakJsonContext` — no Newtonsoft, no generated-DTO mapping on the runtime path
+(issue #18 / the 2026-06-03 STJ-only design). The generated client
+(`KyuzanInc.Peak.PublicApiClient`) is **build-time only**: it is not referenced by
+the SDK at runtime and is not shipped in the package; it exists to detect spec
+drift and to back the `GeneratedDtoContractTests` field-coverage check. The
+generation script still flips all generated types to `internal`; regenerate with
+`scripts/generate-public-api-client.sh` (the internalize step runs automatically
+and the drift job reproduces it). Requests and the public DTO surface are
+hand-written and unchanged.
```

- [ ] **Step 3: `README.md` — Packages table row + consumer response-validation note**

First, the Packages-table row:

```diff
-| `KyuzanInc.Peak.PublicApiClient` | `netstandard2.1;net8.0` | Auto-generated OpenAPI client. `internal` consumers only. Re-exposed by the SDK behind hand-designed DTOs. |
+| `KyuzanInc.Peak.PublicApiClient` | `netstandard2.1;net8.0` | Auto-generated OpenAPI client. Build-time only: backs spec-drift CI and the DTO field-coverage contract test; **not** referenced by the SDK at runtime or shipped in its package. |
```

Then add the consumer caveat the spec requires (§6.3 / R6): the public DTOs are not `[JsonRequired]`, so a missing field defaults silently rather than failing. Insert this note immediately after the Quick start C# example (after the closing ```` ``` ```` of the `csharp` block at `README.md:71`, before the `For maintainers, see …` paragraph):

````markdown
> **Response validation.** The SDK returns server responses as nullable DTOs and
> does not enforce field presence: a missing field deserializes to its default
> (`null` / `0`), not an error. Before you display or transact on identity and
> address fields — notably `AccountResponse.Id` and `AccountAddressResponse.Address`
> — null/empty-check them. A silently empty/defaulted address is a fund-loss-shaped
> failure if trusted blindly. (A future `[JsonRequired]` hardening pass is the
> recommended fix if you want the SDK itself to fail closed — spec §6.3 / R6.)
````

- [ ] **Step 4: `plans/plans-peak-sdk-csharp.md` — W0c / W0d / W0e rows**

Update the W0c row's "Evidence" cell so it reflects the revert (find the row whose ID cell is `W0c`):

```diff
-| W0c | OpenAPI sync workflow + drift CI (snapshot tracks peak-server `main`) | ✅ Done | `scripts/generate-public-api-client.sh` + `openapi-client-drift` CI job; internal `KyuzanInc.Peak.PublicApiClient` generated from `peak-server` `main` HEAD (recorded as a commit SHA per sync). Consumed by the SDK as of PR #14 (`a010bbe`): generated response DTOs are deserialized internally and mapped to the public surface in `src/Mapping/` and the services, never exposed publicly. | #12 (`6196b0e`), SDK wiring #14 (`a010bbe`) | Codex D-P1 |
+| W0c | OpenAPI sync workflow + drift CI (snapshot tracks peak-server `main`) | ✅ Done | `scripts/generate-public-api-client.sh` + `openapi-client-drift` CI job; internal `KyuzanInc.Peak.PublicApiClient` generated from `peak-server` `main` HEAD (recorded as a commit SHA per sync). **Build-time + drift-only**: the SDK's runtime response path was reverted to System.Text.Json-only per issue #18 (the 2026-06-03 design); the generated client now backs spec-drift CI and the `GeneratedDtoContractTests` field-coverage check, and is not referenced by the SDK at runtime or shipped. | #12 (`6196b0e`), SDK wiring #14 (`a010bbe`), STJ revert #18 | Codex D-P1 |
```

Retire the W0d and W0e follow-up rows (both moot under issue #18 — Newtonsoft is off the runtime path and nothing is embedded):

```diff
-| W0d | Unity `link.xml`: preserve `Newtonsoft.Json` and the generated `KyuzanInc.Peak.PublicApiClient.Model` namespace in `peak-sdk-csharp-unity` so IL2CPP stripping keeps them | ⬜ Follow-up | spec R1/OQ2 | — | consume-generated-openapi-client |
-| W0e | Slim the generated client to models-only once a generator-core bump can emit dependency-light DTOs, dropping RestSharp/Polly; also revisit request-DTO adoption | ⬜ Follow-up | spec R2/OQ3 | — | consume-generated-openapi-client |
+| W0d | ~~Unity `link.xml` to preserve Newtonsoft + generated `Model` namespace under IL2CPP~~ | 🟢 Retired | Moot under issue #18: Newtonsoft and the generated client are off the SDK's runtime/package path (2026-06-03 STJ-only design). | — | superseded by #18 |
+| W0e | ~~Slim the generated client to models-only (drop RestSharp/Polly)~~ | 🟢 Retired | Moot under issue #18: the generated client is no longer embedded or referenced at runtime, so its runtime deps never reach a consumer. | — | superseded by #18 |
```

Then verify the STJ-version line elsewhere in the file is already consistent:

Run: `grep -n "System.Text.Json" plans/plans-peak-sdk-csharp.md`
Expected: any version mention reads `8.0.5` (the spec notes line ~173 already says 8.0.5; if any still says 10.0.8, change it to 8.0.5).

- [ ] **Step 5: Sanity-check the docs build / links and run the full suite once more**

Run (scoped to the four edited files — recursing into `docs/` would surface the historical `docs/superpowers/{specs,plans}/*` that legitimately describe the old Newtonsoft/generated path and this design's own change log): `grep -n "GeneratedDtoMappers\|TolerantEnumContractResolver\|Newtonsoft" docs/architecture.md docs/sync-rules.md README.md plans/plans-peak-sdk-csharp.md`
Expected: no match claims the SDK runtime path uses Newtonsoft or the mappers (the only acceptable matches would describe the generated client's own build-time use, which these four files no longer do after this task).

Run: `dotnet test peak-sdk-csharp.sln -c Release --filter "Category!=E2E"`
Expected: PASS (full non-E2E suite, final green check before shipping).

- [ ] **Step 6: Commit**

```bash
git add docs/architecture.md docs/sync-rules.md README.md plans/plans-peak-sdk-csharp.md
git commit -m "docs: reflect STJ-only consumer path; retire Unity link.xml / slim-client follow-ups (#18)"
```

---

## Self-review (run before handoff)

**Spec coverage** — every spec section maps to a task:
- §6.1 (services → public DTOs, envelope, GetAddressDetail/Export ordering) → Tasks 1–3.
- §6.2 (PeakResponseJson STJ-only, transport catch) → Task 4.
- §6.3 / R6 (required-field loosening + consumer caveat) → `GetAsync_MissingRequiredFields_SilentlyDefault` (Task 4) **and** the README "Response validation" note (Task 8 Step 3); R8 `accountIndex` decimal-token narrowing → `GetAsync_DecimalAccountIndexToken_MapsToInvalidResponse` (Task 4).
- §6.4 (csproj/props/locks, STJ 8.0.5) → Task 5.
- §6.5 (delete mappers/resolver/mapper-test; keep internalization test) → Task 4 Step 6 + the "Kept" note.
- §6.6 (request path/Turnkey unchanged) → preserved (no edits to stamps); `TurnkeyWireFormatSmokeTests` stays green (Task 4/5 Step 7/6).
- §6.7 (P0 contract test, friend-attr removal) → Task 6 + Task 5 Step 2.
- §6.8 (AOT guarantees) → PeakResponseJson throw-on-unregistered (Task 4) + contract test (Task 6) + STJ smoke (Task 7).
- §6.9 (CI graph + nuspec both TFMs; net8.0 execution smoke) → Task 7.
- §6.10 (public surface + envelope negative assertion) → Task 6 Step 3.
- §6.11 (doc edits) → Task 8.

**Placeholder scan:** no "TBD"/"add error handling"/"similar to Task N" — every code step shows full code; the only "investigate" instruction (Task 6 Step 2) is a genuine TDD branch for a guard test, with the concrete action spelled out.

**Type consistency:** `UpdateAccountDisplayNameEnvelope` (defined Task 2, used Tasks 2 + 6); `PeakResponseJson.Deserialize<T>` signature unchanged (Task 4); `GetAddressDetailAsync` keeps its public return type across Tasks 2–3; service public method signatures unchanged throughout (so `PeakClient` needs no edits). `Category`/`PublicWireFields`/`GeneratedWireFields` helper names are internal to the contract test and consistent.
