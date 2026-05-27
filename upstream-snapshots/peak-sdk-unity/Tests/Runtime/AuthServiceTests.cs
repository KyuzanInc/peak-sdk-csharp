using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using Peak;
using Peak.Models.Api;
using Peak.Models.Sdk;
using Peak.Services.Unauthenticated;
using Peak.Utils;

namespace Peak.Tests
{
    public class AuthServiceTests
    {
        private const string ApiUrl = "https://api.test";
        private const string ProjectApiKey = "test-api-key";

        private IPeakHttpClient httpClient;
        private AuthService authService;

        [SetUp]
        public void SetUp()
        {
            httpClient = Substitute.For<IPeakHttpClient>();
            authService = new AuthService(ApiUrl, ProjectApiKey, httpClient, () => new TurnkeyUtils.P256KeyPair
            {
                privateKey = "priv",
                publicKey = "pub",
                publicKeyUncompressed = "pub-uncompressed"
            });
        }

        [Test]
        public async System.Threading.Tasks.Task InitOtpLoginAsync_ReturnsOtpId()
        {
            var expected = new InitOtpLoginResponse { otpId = "otp-123" };

            httpClient
                .PostAsync<InitOtpLoginResponse>(
                    "public-api/v1/auth/otp/init-login",
                    Arg.Any<InitOtpLoginRequest>(),
                    null)
                .Returns(UniTask.FromResult(expected));

            var result = await authService.InitOtpLoginAsync("user@example.com");

            Assert.AreEqual("otp-123", result.otpId);
            httpClient.Received(1).PostAsync<InitOtpLoginResponse>(
                "public-api/v1/auth/otp/init-login",
                Arg.Is<InitOtpLoginRequest>(req => req.email == "user@example.com"),
                null);
        }

        [Test]
        public async System.Threading.Tasks.Task CompleteOtpLoginAsync_ReturnsSessionAndKeyPair()
        {
            CompleteOtpLoginRequest capturedRequest = null;
            var response = new CompleteOtpLoginResponse
            {
                user = new UserResponse { id = "user-1" },
                sessionJwt = "session-token",
                isNewUser = false
            };

            httpClient
                .PostAsync<CompleteOtpLoginResponse>(
                    "public-api/v1/auth/otp/complete-login",
                    Arg.Do<object>(payload => capturedRequest = payload as CompleteOtpLoginRequest),
                    null)
                .Returns(UniTask.FromResult(response));

            var result = await authService.CompleteOtpLoginAsync("user@example.com", "otp-123", "654321");

            Assert.IsNotNull(capturedRequest);
            Assert.AreEqual("user@example.com", capturedRequest.email);
            Assert.AreEqual("otp-123", capturedRequest.otpId);
            Assert.AreEqual("654321", capturedRequest.otpCode);
            Assert.AreEqual("pub", capturedRequest.targetPublicKey);
            Assert.IsTrue(capturedRequest.signup); // default is true

            Assert.AreEqual("session-token", result.sessionJwt);
            Assert.IsFalse(result.isNewUser);
            Assert.AreEqual("priv", result.keyPair.privateKey);
            Assert.AreEqual("pub", result.keyPair.publicKey);
        }

        [Test]
        public async System.Threading.Tasks.Task CompleteOtpLoginAsync_WithSignupFalse_SendsSignupFalse()
        {
            CompleteOtpLoginRequest capturedRequest = null;
            var response = new CompleteOtpLoginResponse
            {
                user = new UserResponse { id = "user-1" },
                sessionJwt = "session-token",
                isNewUser = false
            };

            httpClient
                .PostAsync<CompleteOtpLoginResponse>(
                    "public-api/v1/auth/otp/complete-login",
                    Arg.Do<object>(payload => capturedRequest = payload as CompleteOtpLoginRequest),
                    null)
                .Returns(UniTask.FromResult(response));

            var result = await authService.CompleteOtpLoginAsync("user@example.com", "otp-123", "654321", signup: false);

            Assert.IsNotNull(capturedRequest);
            Assert.IsFalse(capturedRequest.signup);
        }

        [Test]
        public async System.Threading.Tasks.Task CompleteOtpLoginAsync_WithNewUser_ReturnsAccountInfo()
        {
            var response = new CompleteOtpLoginResponse
            {
                user = new UserResponse { id = "user-1" },
                sessionJwt = "session-token",
                isNewUser = true,
                accountSource = new AccountSourceResponse { id = "source-1" },
                account = new AccountResponse { id = "account-1" },
                accountAddresses = new[] { new AccountAddressResponse { id = "addr-1", address = "0xabc" } }
            };

            httpClient
                .PostAsync<CompleteOtpLoginResponse>(
                    "public-api/v1/auth/otp/complete-login",
                    Arg.Any<CompleteOtpLoginRequest>(),
                    null)
                .Returns(UniTask.FromResult(response));

            var result = await authService.CompleteOtpLoginAsync("user@example.com", "otp-123", "654321");

            Assert.IsTrue(result.isNewUser);
            Assert.IsNotNull(result.accountSource);
            Assert.AreEqual("source-1", result.accountSource.id);
            Assert.IsNotNull(result.account);
            Assert.AreEqual("account-1", result.account.id);
            Assert.IsNotNull(result.accountAddresses);
            Assert.AreEqual("0xabc", result.accountAddresses[0].address);
        }
    }
}
