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

        private const string PrivateKeySourceDetailJson =
            "{\"accountAddress\":{\"id\":\"ad1\",\"accountId\":\"acc1\",\"address\":\"0xabc\",\"chainType\":\"evm\"}," +
            "\"account\":{\"id\":\"acc1\",\"userId\":\"u1\",\"accountSourceId\":\"src1\",\"accountIndex\":0,\"originProjectId\":\"proj1\"}," +
            "\"accountSource\":{\"id\":\"s1\",\"userId\":\"u1\",\"originProjectId\":\"proj1\",\"turnkeyResourceId\":\"tk1\",\"sourceType\":\"private-key\",\"creationMethod\":\"imported\"}}";

        [Fact]
        public async Task Export_PrivateKeySource_MapsBundleFromGeneratedDto()
        {
            var http = Substitute.For<IPeakHttpClient>();
            var addressDetail = PeakResponseJson.Deserialize<Gen.GetAccountAddressWithAccountAndSourceResponseDto>(PrivateKeySourceDetailJson)!;
            http.GetAsync<Gen.GetAccountAddressWithAccountAndSourceResponseDto>(
                    Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(addressDetail);
            var exportDto = PeakResponseJson.Deserialize<Gen.ExportPrivateKeyResponseDto>("{\"exportBundle\":\"bundle-x\"}")!;
            http.PostAsync<ExportPrivateKeyRequest, Gen.ExportPrivateKeyResponseDto>(
                    "public-api/v1/private-keys/export",
                    Arg.Any<ExportPrivateKeyRequest>(),
                    Arg.Any<IReadOnlyDictionary<string, string>>(),
                    Arg.Any<CancellationToken>())
                .Returns(exportDto);

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
            // An unknown sourceType maps to null via the tolerant enum resolver, so
            // the export flow hits the unsupported-source-type branch (no signing).
            var http = Substitute.For<IPeakHttpClient>();
            var addressDetail = PeakResponseJson.Deserialize<Gen.GetAccountAddressWithAccountAndSourceResponseDto>(
                "{\"accountAddress\":{\"id\":\"ad1\",\"accountId\":\"acc1\",\"address\":\"0xabc\",\"chainType\":\"evm\"}," +
                "\"account\":{\"id\":\"acc1\",\"userId\":\"u1\",\"accountSourceId\":\"src1\",\"accountIndex\":0,\"originProjectId\":\"proj1\"}," +
                "\"accountSource\":{\"id\":\"s1\",\"userId\":\"u1\",\"originProjectId\":\"proj1\",\"turnkeyResourceId\":\"tk1\",\"sourceType\":\"brand-new-source\",\"creationMethod\":\"imported\"}}")!;
            http.GetAsync<Gen.GetAccountAddressWithAccountAndSourceResponseDto>(
                    Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(addressDetail);

            // A real P-256 key: the service builds the Turnkey client before the
            // source-type branch, so a dummy key would throw earlier. The unknown
            // source type is what must surface as PeakError InvalidResponse.
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
