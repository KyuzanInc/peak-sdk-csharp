using System;
using Cysharp.Threading.Tasks;
using Peak.Models.Api;
using Peak.Models.Sdk;
using Peak.Services;
using Peak.Utils;
using UnityEngine;

namespace Peak.Services.Unauthenticated
{
    public class AuthService
    {
        private readonly IPeakHttpClient httpClient;
        private readonly Func<TurnkeyUtils.P256KeyPair> keyPairFactory;

        public AuthService(string apiUrl, string projectApiKey, IPeakHttpClient httpClient = null, Func<TurnkeyUtils.P256KeyPair> keyPairFactory = null)
        {
            this.httpClient = httpClient ?? new PeakHttpClient(apiUrl, projectApiKey);
            this.keyPairFactory = keyPairFactory ?? TurnkeyUtils.GenerateP256KeyPair;
        }

        public async UniTask<InitOtpLoginResponse> InitOtpLoginAsync(string email)
        {
            Debug.Log($"[AuthService] Starting init OTP login for: {email}");

            var payload = new InitOtpLoginRequest(email);
            var response = await httpClient.PostAsync<InitOtpLoginResponse>("public-api/v1/auth/otp/init-login", payload);

            Debug.Log($"[AuthService] Init OTP login successful - OTP ID: {response.otpId}");
            return response;
        }

        public async UniTask<CompleteOtpLoginResult> CompleteOtpLoginAsync(string email, string otpId, string otpCode, bool signup = true)
        {
            try
            {
                Debug.Log($"[AuthService] Starting complete OTP login for: {email} (signup={signup})");

                var keyPair = keyPairFactory?.Invoke();
                if (keyPair == null)
                {
                    throw new InvalidOperationException("Failed to generate P256 key pair for OTP login");
                }

                var payload = new CompleteOtpLoginRequest(
                    email,
                    otpId,
                    otpCode,
                    keyPair.publicKey,
                    signup
                );

                var response = await httpClient.PostAsync<CompleteOtpLoginResponse>("public-api/v1/auth/otp/complete-login", payload);

                Debug.Log($"[AuthService] Complete OTP login successful - Session JWT received (isNewUser={response.isNewUser})");

                return new CompleteOtpLoginResult
                {
                    user = response.user,
                    sessionJwt = response.sessionJwt,
                    isNewUser = response.isNewUser,
                    accountSource = response.accountSource,
                    account = response.account,
                    accountAddresses = response.accountAddresses,
                    keyPair = new KeyPair
                    {
                        privateKey = keyPair.privateKey,
                        publicKey = keyPair.publicKey
                    }
                };
            }
            catch (Exception e)
            {
                Debug.LogError($"[AuthService] Complete OTP login failed: {e.Message}");
                throw;
            }
        }
    }
}
