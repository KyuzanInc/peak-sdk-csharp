using System;

namespace Peak.Models.Api
{
    /// <summary>
    /// Response DTO for POST /public-api/v1/auth/signup endpoint.
    /// </summary>
    [Serializable]
    public class SignupResponse
    {
        /// <summary>
        /// Created Peak User
        /// </summary>
        public UserResponse user;

        /// <summary>
        /// Created account source
        /// </summary>
        public AccountSourceResponse accountSource;

        /// <summary>
        /// Created default account
        /// </summary>
        public AccountResponse account;

        /// <summary>
        /// Created account addresses (EVM, Bitcoin, Solana, Sui)
        /// </summary>
        public AccountAddressResponse[] accountAddresses;
    }

    /// <summary>
    /// Response DTO for POST /public-api/v1/auth/init-login endpoint.
    /// </summary>
    [Serializable]
    public class InitOtpLoginResponse
    {
        /// <summary>
        /// OTP ID for the login flow
        /// </summary>
        public string otpId;
    }

    /// <summary>
    /// Response DTO for POST /public-api/v1/auth/complete-login endpoint.
    /// </summary>
    [Serializable]
    public class CompleteOtpLoginResponse
    {
        /// <summary>
        /// Authenticated user information
        /// </summary>
        public UserResponse user;

        /// <summary>
        /// Session JWT token
        /// </summary>
        public string sessionJwt;

        /// <summary>
        /// True if the user was created during this login (signup=true and user was missing)
        /// </summary>
        public bool isNewUser;

        /// <summary>
        /// Account source (only present when isNewUser is true)
        /// </summary>
        public AccountSourceResponse accountSource;

        /// <summary>
        /// Default account (only present when isNewUser is true)
        /// </summary>
        public AccountResponse account;

        /// <summary>
        /// Account addresses (only present when isNewUser is true)
        /// </summary>
        public AccountAddressResponse[] accountAddresses;
    }
}
