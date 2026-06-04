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
