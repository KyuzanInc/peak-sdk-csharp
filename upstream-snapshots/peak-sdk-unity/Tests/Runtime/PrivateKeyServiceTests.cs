using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using Peak;
using Peak.Models.Api;
using Peak.Models.Sdk;
using Peak.Services.Authenticated;
using Peak.Utils;

namespace Peak.Tests
{
    public class PrivateKeyServiceTests
    {
        private const string ApiUrl = "https://api.example.com";
        private const string ProjectApiKey = "project-key";
        private const string SessionJwt = "session-jwt";
        private const string TargetPrivateKey = "4d3f2abf13ab5d8f2c1b0a4e8f3d6c7b8a9d0e1f2c3b4a596877665544332211";

        [Test]
        public void CompleteImportPrivateKeyAsync_WithEmptyEncryptedBundle_Throws()
        {
            var httpClient = Substitute.For<IPeakHttpClient>();
            var service = new PrivateKeyService(ApiUrl, ProjectApiKey, SessionJwt, TargetPrivateKey, httpClient);

            Assert.ThrowsAsync<ArgumentException>(async () => await service.CompleteImportPrivateKeyAsync(string.Empty, "evm"));
        }

        [Test]
        public void CompleteImportPrivateKeyAsync_WithEmptyChainType_Throws()
        {
            var httpClient = Substitute.For<IPeakHttpClient>();
            var service = new PrivateKeyService(ApiUrl, ProjectApiKey, SessionJwt, TargetPrivateKey, httpClient);

            Assert.ThrowsAsync<ArgumentException>(async () => await service.CompleteImportPrivateKeyAsync("encryptedBundle", string.Empty));
        }

        [Test]
        public void CompleteImportPrivateKeyAsync_WithUnsupportedChainType_Throws()
        {
            var httpClient = Substitute.For<IPeakHttpClient>();
            var service = new PrivateKeyService(ApiUrl, ProjectApiKey, SessionJwt, TargetPrivateKey, httpClient);

            Assert.ThrowsAsync<ArgumentException>(async () => await service.CompleteImportPrivateKeyAsync("encryptedBundle", "bitcoin"));
        }

        [Test]
        public void ExportPrivateKeyAsync_WithEmptyAddress_Throws()
        {
            var httpClient = Substitute.For<IPeakHttpClient>();
            var service = new PrivateKeyService(ApiUrl, ProjectApiKey, SessionJwt, TargetPrivateKey, httpClient);

            Assert.ThrowsAsync<ArgumentException>(async () => await service.ExportPrivateKeyAsync(string.Empty, "targetPublicKey"));
        }

        [Test]
        public void ExportPrivateKeyAsync_WithEmptyTargetPublicKey_Throws()
        {
            var httpClient = Substitute.For<IPeakHttpClient>();
            var service = new PrivateKeyService(ApiUrl, ProjectApiKey, SessionJwt, TargetPrivateKey, httpClient);

            Assert.ThrowsAsync<ArgumentException>(async () => await service.ExportPrivateKeyAsync("0x1234", string.Empty));
        }
    }
}
