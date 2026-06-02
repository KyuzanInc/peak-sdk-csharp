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

        [Fact]
        public async Task CompleteOtpLogin_MapsResultAndCarriesKeyPair()
        {
            var http = Substitute.For<IPeakHttpClient>();
            var dto = PeakResponseJson.Deserialize<Gen.CompleteOtpLoginResponseDto>(
                "{\"user\":{\"id\":\"u1\",\"email\":\"a@b.c\",\"originProjectId\":\"p1\"," +
                "\"turnkeySubOrgId\":\"s1\",\"turnkeyRootUserId\":\"r1\",\"deletionStatus\":\"none\"}," +
                "\"sessionJwt\":\"jwt-x\",\"isNewUser\":true}")!;
            http.PostAsync<CompleteOtpLoginRequest, Gen.CompleteOtpLoginResponseDto>(
                    "public-api/v1/auth/otp/complete-login",
                    Arg.Any<CompleteOtpLoginRequest>(),
                    Arg.Any<IReadOnlyDictionary<string, string>>(),
                    Arg.Any<CancellationToken>())
                .Returns(dto);

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
