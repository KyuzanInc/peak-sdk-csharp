using System;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace KyuzanInc.Turnkey.Sdk.Tests
{
    public class HttpTests
    {
        private const string TestPrivateKey = "31aa1fbcca6c30aebb5e87e85d8c93f0e8b13a3a8f51e0a8c5fe6a3a4a14a91b";

        [Fact]
        public void FromTargetPrivateKey_Rejects_EmptyKey()
        {
            Action act = () => Http.FromTargetPrivateKey("");
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void FromTargetPrivateKey_Rejects_InvalidHex()
        {
            Action act = () => Http.FromTargetPrivateKey("zz");
            // Encoding.Uint8ArrayFromHexString throws an ArgumentException for invalid hex.
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void StampGetWhoami_RejectsEmptyOrganizationId()
        {
            var client = Http.FromTargetPrivateKey(TestPrivateKey);
            Action act = () => client.StampGetWhoami("");
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void StampGetWhoami_ProducesValidSignedRequest()
        {
            var client = Http.FromTargetPrivateKey(TestPrivateKey);
            var req = client.StampGetWhoami("org-123");

            req.Url.Should().Be("https://api.turnkey.com/public/v1/query/whoami");
            req.Body.Should().NotBeNullOrEmpty();
            req.Stamp.Should().NotBeNull();
            req.Stamp!.StampHeaderName.Should().Be("X-Stamp");
            req.Stamp.StampHeaderValue.Should().NotBeNullOrEmpty();

            // Body is camelCase JSON with the organizationId field.
            using var doc = JsonDocument.Parse(req.Body!);
            doc.RootElement.GetProperty("organizationId").GetString().Should().Be("org-123");
        }

        [Fact]
        public void StampInitImportPrivateKey_Rejects_NullBody()
        {
            var client = Http.FromTargetPrivateKey(TestPrivateKey);
            Action act = () => client.StampInitImportPrivateKey(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void StampInitImportPrivateKey_ProducesExpectedUrlAndBody()
        {
            var client = Http.FromTargetPrivateKey(TestPrivateKey);
            var body = new Http.InitImportPrivateKeyRequestBody
            {
                OrganizationId = "org-123",
                Type = "ACTIVITY_TYPE_INIT_IMPORT_PRIVATE_KEY",
                TimestampMs = "1700000000000",
                Parameters = new Http.InitImportPrivateKeyParameters { UserId = "user-1" },
            };
            var req = client.StampInitImportPrivateKey(body);

            req.Url.Should().Be("https://api.turnkey.com/public/v1/submit/init_import_private_key");
            using var doc = JsonDocument.Parse(req.Body!);
            doc.RootElement.GetProperty("organizationId").GetString().Should().Be("org-123");
            doc.RootElement.GetProperty("type").GetString().Should().Be("ACTIVITY_TYPE_INIT_IMPORT_PRIVATE_KEY");
            doc.RootElement.GetProperty("timestampMs").GetString().Should().Be("1700000000000");
            doc.RootElement.GetProperty("parameters").GetProperty("userId").GetString().Should().Be("user-1");
        }

        [Fact]
        public void StampImportPrivateKey_ProducesExpectedUrl()
        {
            var client = Http.FromTargetPrivateKey(TestPrivateKey);
            var req = client.StampImportPrivateKey(new Http.ImportPrivateKeyRequestBody
            {
                OrganizationId = "org-123",
                Type = "T",
                TimestampMs = "1",
                Parameters = new Http.ImportPrivateKeyParameters
                {
                    UserId = "u",
                    Curve = "CURVE_SECP256K1",
                    AddressFormats = new[] { "ADDRESS_FORMAT_ETHEREUM" },
                    EncryptedBundle = "ab",
                    PrivateKeyName = "name",
                },
            });
            req.Url.Should().Be("https://api.turnkey.com/public/v1/submit/import_private_key");
        }

        [Fact]
        public void StampExportPrivateKey_ProducesExpectedUrl()
        {
            var client = Http.FromTargetPrivateKey(TestPrivateKey);
            var req = client.StampExportPrivateKey(new Http.ExportPrivateKeyRequestBody
            {
                OrganizationId = "org-123",
                Type = "T",
                TimestampMs = "1",
                Parameters = new Http.ExportPrivateKeyParameters
                {
                    PrivateKeyId = "pk-1",
                    TargetPublicKey = "ab",
                },
            });
            req.Url.Should().Be("https://api.turnkey.com/public/v1/submit/export_private_key");
        }

        [Fact]
        public void StampExportWalletAccount_ProducesExpectedUrl()
        {
            var client = Http.FromTargetPrivateKey(TestPrivateKey);
            var req = client.StampExportWalletAccount(new Http.ExportWalletAccountRequestBody
            {
                OrganizationId = "org-123",
                Type = "T",
                TimestampMs = "1",
                Parameters = new Http.ExportWalletAccountParameters
                {
                    Address = "0xabc",
                    TargetPublicKey = "ab",
                },
            });
            req.Url.Should().Be("https://api.turnkey.com/public/v1/submit/export_wallet_account");
        }

        [Fact]
        public void GetHttpClient_RejectsEmptyBundle()
        {
            Action act = () => Http.GetHttpClient("", TestPrivateKey);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void GetHttpClient_RejectsEmptyPrivateKey()
        {
            Action act = () => Http.GetHttpClient("dummy", "");
            act.Should().Throw<ArgumentException>();
        }
    }
}
