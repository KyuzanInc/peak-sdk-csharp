using System;
using FluentAssertions;
using KyuzanInc.Peak.Sdk.Utils;
using Xunit;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class SessionJwtTests
    {
        [Fact]
        public void DecodeSessionJwt_RejectsMalformed()
        {
            Action act = () => SessionJwt.DecodeSessionJwt("notajwt");
            act.Should().Throw<PeakError>().Which.Code.Should().Be(PeakErrorCode.InvalidJwt);
        }

        [Fact]
        public void DecodeSessionJwt_RejectsEmptyPayload()
        {
            // Two-part JWT (header.only). Decoder requires at least header.payload.
            Action act = () => SessionJwt.DecodeSessionJwt("only.one");
            act.Should().Throw<PeakError>().Which.Code.Should().Be(PeakErrorCode.InvalidJwt);
        }

        [Fact]
        public void DecodeSessionJwt_HappyPath()
        {
            // Hand-build a valid JWT payload. We don't verify the signature here;
            // DecodeSessionJwt is a pure decoder.
            var payload = "{\"exp\":1900000000,\"organization_id\":\"org\",\"public_key\":\"pk\",\"session_type\":\"OTP\",\"user_id\":\"user\"}";
            var b64 = ToBase64Url(System.Text.Encoding.UTF8.GetBytes(payload));
            var jwt = $"hdr.{b64}.sig";

            var decoded = SessionJwt.DecodeSessionJwt(jwt);
            decoded.Expiry.Should().Be(1900000000);
            decoded.TurnkeySubOrgId.Should().Be("org");
            decoded.PublicKey.Should().Be("pk");
            decoded.SessionType.Should().Be("OTP");
            decoded.TurnkeyUserId.Should().Be("user");
        }

        [Fact]
        public void DecodeSessionJwt_RejectsMissingRequiredField()
        {
            // Missing user_id.
            var payload = "{\"exp\":1900000000,\"organization_id\":\"org\",\"public_key\":\"pk\",\"session_type\":\"OTP\"}";
            var b64 = ToBase64Url(System.Text.Encoding.UTF8.GetBytes(payload));
            var jwt = $"hdr.{b64}.sig";

            Action act = () => SessionJwt.DecodeSessionJwt(jwt);
            act.Should().Throw<PeakError>().Which.Code.Should().Be(PeakErrorCode.InvalidJwt);
        }

        [Fact]
        public void IsExpired_TrueWhenPast()
        {
            var payload = new SessionJwtPayload { Expiry = 100 }; // long past
            SessionJwt.IsExpired(payload).Should().BeTrue();
        }

        [Fact]
        public void IsExpired_FalseWhenFuture()
        {
            var payload = new SessionJwtPayload
            {
                Expiry = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600,
            };
            SessionJwt.IsExpired(payload).Should().BeFalse();
        }

        private static string ToBase64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }
    }
}
