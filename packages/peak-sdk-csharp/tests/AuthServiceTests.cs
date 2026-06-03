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
