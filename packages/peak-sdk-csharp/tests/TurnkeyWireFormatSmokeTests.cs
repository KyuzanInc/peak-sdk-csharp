// Smoke test that pins the wire-format expectations peak-sdk-csharp
// places on the external KyuzanInc.Turnkey.Sdk package. If the
// upstream package renames a field, drops a method, or changes its
// JSON shape, these tests fail before consumers do — which gives the
// docs/sync-rules.md "Bump KyuzanInc.Turnkey.Sdk" workflow something
// concrete to fail on.
//
// Each block names the peak-sdk-csharp consumption site it protects.

using System.Text.Json;
using FluentAssertions;
using KyuzanInc.Peak.Sdk.Models;
using Xunit;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class TurnkeyWireFormatSmokeTests
    {
        // A valid 32-byte hex P-256 private key. Generated once, stored
        // as a constant so the test is reproducible. NOT a real key.
        // (Crypto.GenerateP256KeyPair() also gives us a fresh one at
        // runtime; we hard-code one for stable JSON snapshots.)
        private const string TestTargetPrivateKeyHex =
            "1111111111111111111111111111111111111111111111111111111111111111";

        // ------------------------------------------------------------------
        // Crypto surface — used by AuthService and SessionJwt
        // ------------------------------------------------------------------

        [Fact]
        public void Crypto_GenerateP256KeyPair_ReturnsHexEncodedKeys()
        {
            // Consumed by AuthService.CompleteOtpLoginAsync via keyPairFactory.
            var kp = global::Turnkey.Crypto.GenerateP256KeyPair();

            kp.Should().NotBeNull();
            kp.PrivateKey.Should().NotBeNullOrEmpty();
            kp.PublicKey.Should().NotBeNullOrEmpty();

            // 32-byte private key = 64 hex chars.
            kp.PrivateKey.Length.Should().Be(64);
            kp.PrivateKey.Should().MatchRegex("^[0-9a-fA-F]+$");

            // Compressed P-256 public key = 33 bytes = 66 hex chars.
            // Uncompressed = 65 bytes = 130 hex chars. Either is fine,
            // we only care that it parses as hex and is non-empty.
            kp.PublicKey.Should().MatchRegex("^[0-9a-fA-F]+$");
        }

        [Fact]
        public void Crypto_VerifySessionJwtSignature_RejectsMalformed()
        {
            // Consumed by SessionJwt.VerifySessionJwt. The Peak wrapper
            // already normalises both shapes (return-false and throw)
            // into a PeakError, so the contract this smoke test pins
            // is "malformed input is rejected" rather than the exact
            // exception/return signal. Either signal satisfies the
            // consumer; both let SessionJwt.VerifySessionJwt do its
            // job.
            bool rejected;
            try
            {
                rejected = !global::Turnkey.Crypto.VerifySessionJwtSignature("not-a-jwt");
            }
            catch
            {
                rejected = true;
            }
            rejected.Should().BeTrue("Turnkey must not accept a malformed JWT");
        }

        // ------------------------------------------------------------------
        // Http construction — used by PrivateKeyService.CreateTurnkeyClient
        // ------------------------------------------------------------------

        [Fact]
        public void Http_FromTargetPrivateKey_ConstructsWithoutThrowing()
        {
            // Consumed by PrivateKeyService.CreateTurnkeyClient.
            var client = global::Turnkey.Http.FromTargetPrivateKey(TestTargetPrivateKeyHex);
            client.Should().NotBeNull();
        }

        // ------------------------------------------------------------------
        // Stamped activity request bodies — every Http.Stamp* method
        // peak-sdk-csharp invokes today. We assert that the body JSON
        // contains the camelCase field names the Turnkey API expects.
        // ------------------------------------------------------------------

        [Fact]
        public void StampGetWhoami_BodyShapeIsCorrect()
        {
            // Not consumed by peak-sdk-csharp today, but listed in the
            // package's documented surface. Asserted so a drop or
            // signature change is caught at upgrade time.
            var client = global::Turnkey.Http.FromTargetPrivateKey(TestTargetPrivateKeyHex);

            var signed = client.StampGetWhoami("org-123");

            AssertSignedRequestShape(signed);

            using var doc = JsonDocument.Parse(signed.Body!);
            doc.RootElement.GetProperty("organizationId").GetString().Should().Be("org-123");
        }

        [Fact]
        public void StampInitImportPrivateKey_BodyShapeIsCorrect()
        {
            // Consumed by PrivateKeyService.InitImportPrivateKeyAsync.
            var client = global::Turnkey.Http.FromTargetPrivateKey(TestTargetPrivateKeyHex);
            var body = new global::Turnkey.Http.InitImportPrivateKeyRequestBody
            {
                OrganizationId = "org-123",
                Type = "ACTIVITY_TYPE_INIT_IMPORT_PRIVATE_KEY",
                TimestampMs = "1700000000000",
                Parameters = new global::Turnkey.Http.InitImportPrivateKeyParameters
                {
                    UserId = "user-abc",
                },
            };

            var signed = client.StampInitImportPrivateKey(body);

            AssertSignedRequestShape(signed);

            using var doc = JsonDocument.Parse(signed.Body!);
            var root = doc.RootElement;
            root.GetProperty("organizationId").GetString().Should().Be("org-123");
            root.GetProperty("type").GetString().Should().Be("ACTIVITY_TYPE_INIT_IMPORT_PRIVATE_KEY");
            root.GetProperty("timestampMs").GetString().Should().Be("1700000000000");
            root.GetProperty("parameters").GetProperty("userId").GetString().Should().Be("user-abc");
        }

        [Fact]
        public void StampImportPrivateKey_BodyShapeIsCorrect()
        {
            // Consumed by PrivateKeyService.CompleteImportPrivateKeyAsync.
            var client = global::Turnkey.Http.FromTargetPrivateKey(TestTargetPrivateKeyHex);
            var body = new global::Turnkey.Http.ImportPrivateKeyRequestBody
            {
                OrganizationId = "org-123",
                Type = "ACTIVITY_TYPE_IMPORT_PRIVATE_KEY",
                TimestampMs = "1700000000000",
                Parameters = new global::Turnkey.Http.ImportPrivateKeyParameters
                {
                    UserId = "user-abc",
                    AddressFormats = new[] { "ADDRESS_FORMAT_ETHEREUM" },
                    Curve = "CURVE_SECP256K1",
                    EncryptedBundle = "encrypted-bundle-hex",
                    PrivateKeyName = "Imported Private Key 1700000000000",
                },
            };

            var signed = client.StampImportPrivateKey(body);

            AssertSignedRequestShape(signed);

            using var doc = JsonDocument.Parse(signed.Body!);
            var parameters = doc.RootElement.GetProperty("parameters");
            parameters.GetProperty("userId").GetString().Should().Be("user-abc");
            parameters.GetProperty("curve").GetString().Should().Be("CURVE_SECP256K1");
            parameters.GetProperty("encryptedBundle").GetString().Should().Be("encrypted-bundle-hex");
            parameters.GetProperty("privateKeyName").GetString().Should().Be("Imported Private Key 1700000000000");

            var addressFormats = parameters.GetProperty("addressFormats");
            addressFormats.GetArrayLength().Should().Be(1);
            addressFormats[0].GetString().Should().Be("ADDRESS_FORMAT_ETHEREUM");
        }

        [Fact]
        public void StampExportPrivateKey_BodyShapeIsCorrect()
        {
            // Consumed by PrivateKeyService.ExportPrivateKeyAsync (source-type = private-key).
            var client = global::Turnkey.Http.FromTargetPrivateKey(TestTargetPrivateKeyHex);
            var body = new global::Turnkey.Http.ExportPrivateKeyRequestBody
            {
                OrganizationId = "org-123",
                Type = "ACTIVITY_TYPE_EXPORT_PRIVATE_KEY",
                TimestampMs = "1700000000000",
                Parameters = new global::Turnkey.Http.ExportPrivateKeyParameters
                {
                    PrivateKeyId = "pk-resource-id",
                    TargetPublicKey = "04abcd...",
                },
            };

            var signed = client.StampExportPrivateKey(body);

            AssertSignedRequestShape(signed);

            using var doc = JsonDocument.Parse(signed.Body!);
            var parameters = doc.RootElement.GetProperty("parameters");
            parameters.GetProperty("privateKeyId").GetString().Should().Be("pk-resource-id");
            parameters.GetProperty("targetPublicKey").GetString().Should().Be("04abcd...");
        }

        [Fact]
        public void StampExportWalletAccount_BodyShapeIsCorrect()
        {
            // Consumed by PrivateKeyService.ExportPrivateKeyAsync (source-type = recovery-phrase).
            var client = global::Turnkey.Http.FromTargetPrivateKey(TestTargetPrivateKeyHex);
            var body = new global::Turnkey.Http.ExportWalletAccountRequestBody
            {
                OrganizationId = "org-123",
                Type = "ACTIVITY_TYPE_EXPORT_WALLET_ACCOUNT",
                TimestampMs = "1700000000000",
                Parameters = new global::Turnkey.Http.ExportWalletAccountParameters
                {
                    Address = "0xabc",
                    TargetPublicKey = "04abcd...",
                },
            };

            var signed = client.StampExportWalletAccount(body);

            AssertSignedRequestShape(signed);

            using var doc = JsonDocument.Parse(signed.Body!);
            var parameters = doc.RootElement.GetProperty("parameters");
            parameters.GetProperty("address").GetString().Should().Be("0xabc");
            parameters.GetProperty("targetPublicKey").GetString().Should().Be("04abcd...");
        }

        // ------------------------------------------------------------------
        // PeakJsonContext envelope shape — what peak-server actually
        // receives at the public-api/v1/private-keys/* endpoints.
        // ------------------------------------------------------------------

        [Fact]
        public void PeakJsonContext_InitImportPrivateKeyRequest_HasSignedEnvelopeKey()
        {
            var inner = new global::Turnkey.Http.SignedRequest
            {
                Url = "https://api.turnkey.com/...",
                Body = "{\"foo\":\"bar\"}",
                Stamp = new global::Turnkey.Http.Stamp
                {
                    StampHeaderName = "X-Stamp",
                    StampHeaderValue = "header-value",
                },
            };

            var wrapped = new InitImportPrivateKeyRequest
            {
                SignedInitImportPrivateKeyRequest = inner,
            };

            var json = JsonSerializer.Serialize(wrapped, PeakJsonContext.Default.InitImportPrivateKeyRequest);

            using var doc = JsonDocument.Parse(json);
            doc.RootElement.TryGetProperty("signedInitImportPrivateKeyRequest", out _)
                .Should().BeTrue("camelCase outer key is the peak-server contract");
        }

        [Fact]
        public void PeakJsonContext_CompleteImportPrivateKeyRequest_HasSignedEnvelopeKeyAndChainType()
        {
            var inner = new global::Turnkey.Http.SignedRequest
            {
                Url = "https://api.turnkey.com/...",
                Body = "{}",
                Stamp = new global::Turnkey.Http.Stamp
                {
                    StampHeaderName = "X-Stamp",
                    StampHeaderValue = "h",
                },
            };

            var wrapped = new CompleteImportPrivateKeyRequest
            {
                ChainType = "evm",
                SignedCompleteImportPrivateKeyRequest = inner,
            };

            var json = JsonSerializer.Serialize(wrapped, PeakJsonContext.Default.CompleteImportPrivateKeyRequest);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            root.GetProperty("chainType").GetString().Should().Be("evm");
            root.TryGetProperty("signedCompleteImportPrivateKeyRequest", out _).Should().BeTrue();
        }

        [Fact]
        public void PeakJsonContext_ExportPrivateKeyRequest_HasSignedEnvelopeKeyAndSourceType()
        {
            var inner = new global::Turnkey.Http.SignedRequest
            {
                Url = "https://api.turnkey.com/...",
                Body = "{}",
                Stamp = new global::Turnkey.Http.Stamp
                {
                    StampHeaderName = "X-Stamp",
                    StampHeaderValue = "h",
                },
            };

            var wrapped = new ExportPrivateKeyRequest
            {
                SourceType = "private-key",
                SignedExportPrivateKeyRequest = inner,
            };

            var json = JsonSerializer.Serialize(wrapped, PeakJsonContext.Default.ExportPrivateKeyRequest);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            root.GetProperty("sourceType").GetString().Should().Be("private-key");
            root.TryGetProperty("signedExportPrivateKeyRequest", out _).Should().BeTrue();
        }

        // ------------------------------------------------------------------
        // SignedRequest shape — all four Stamp* outputs go through this.
        // ------------------------------------------------------------------

        private static void AssertSignedRequestShape(global::Turnkey.Http.SignedRequest signed)
        {
            signed.Should().NotBeNull();
            signed.Url.Should().NotBeNullOrEmpty();
            signed.Body.Should().NotBeNullOrEmpty();
            signed.Stamp.Should().NotBeNull();
            signed.Stamp!.StampHeaderName.Should().Be("X-Stamp",
                "peak-server forwards this exact header to Turnkey");
            signed.Stamp!.StampHeaderValue.Should().NotBeNullOrEmpty();
        }
    }
}
