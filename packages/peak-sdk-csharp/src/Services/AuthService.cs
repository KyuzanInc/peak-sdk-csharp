// Ported from upstream-snapshots/peak-sdk-unity/Runtime/Services/Unauthenticated/AuthService.cs.
// Changes:
//   - UniTask<T> -> Task<T>
//   - Debug.Log -> ILogger<AuthService>.LogInformation
//   - Crypto.GenerateP256KeyPair() is called via Turnkey.Crypto in the new
//     KyuzanInc.Turnkey.Sdk package (was TurnkeyUtils.GenerateP256KeyPair).

using System;
using System.Threading;
using System.Threading.Tasks;
using KyuzanInc.Peak.Sdk.Models;
using KyuzanInc.Peak.Sdk.Utils;
using Microsoft.Extensions.Logging;

namespace KyuzanInc.Peak.Sdk.Services
{
    /// <summary>
    /// Service for the OTP login flow (init + complete). Used by
    /// <see cref="PeakClient"/>.
    /// </summary>
    public sealed class AuthService
    {
        private readonly IPeakHttpClient httpClient;
        private readonly Func<global::Turnkey.Crypto.KeyPair> keyPairFactory;
        private readonly ILogger<AuthService> logger;

        public AuthService(
            string apiUrl,
            string projectApiKey,
            IPeakHttpClient? httpClient = null,
            Func<global::Turnkey.Crypto.KeyPair>? keyPairFactory = null,
            ILogger<AuthService>? logger = null)
        {
            this.httpClient = httpClient ?? new DefaultPeakHttpClient(apiUrl, projectApiKey);
            this.keyPairFactory = keyPairFactory ?? global::Turnkey.Crypto.GenerateP256KeyPair;
            this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthService>.Instance;
        }

        public async Task<InitOtpLoginResponse?> InitOtpLoginAsync(string email, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Starting init OTP login for: {Email}", email);
            var payload = new InitOtpLoginRequest { Email = email };
            var response = await httpClient.PostAsync<InitOtpLoginRequest, InitOtpLoginResponse>(
                "public-api/v1/auth/otp/init-login", payload, cancellationToken: cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Init OTP login successful - OTP ID: {OtpId}", response?.OtpId);
            return response;
        }

        public async Task<CompleteOtpLoginResult> CompleteOtpLoginAsync(
            string email, string otpId, string otpCode, bool signup = true, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Starting complete OTP login for: {Email} (signup={Signup})", email, signup);

            var keyPair = keyPairFactory.Invoke()
                ?? throw new PeakError(PeakErrorCode.AuthenticationFailed,
                    "Failed to generate P256 key pair for OTP login");

            var payload = new CompleteOtpLoginRequest
            {
                Email = email,
                OtpId = otpId,
                OtpCode = otpCode,
                TargetPublicKey = keyPair.PublicKey,
                Signup = signup,
            };

            var response = await httpClient.PostAsync<CompleteOtpLoginRequest, CompleteOtpLoginResponse>(
                "public-api/v1/auth/otp/complete-login", payload, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response == null)
            {
                throw new PeakError(PeakErrorCode.AuthenticationFailed,
                    "Complete OTP login response was empty");
            }

            logger.LogInformation("Complete OTP login successful (isNewUser={IsNewUser})", response.IsNewUser);

            return new CompleteOtpLoginResult
            {
                User = response.User,
                SessionJwt = response.SessionJwt,
                IsNewUser = response.IsNewUser,
                AccountSource = response.AccountSource,
                Account = response.Account,
                AccountAddresses = response.AccountAddresses,
                KeyPair = new KeyPair
                {
                    PrivateKey = keyPair.PrivateKey,
                    PublicKey = keyPair.PublicKey,
                },
            };
        }
    }
}
