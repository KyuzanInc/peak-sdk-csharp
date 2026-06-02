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
