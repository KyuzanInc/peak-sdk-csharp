using System;
using Peak.Models.Api;

namespace Peak.Models.Sdk
{
    /// <summary>
    /// Result returned by AuthService after completing the OTP login flow.
    /// Contains API response data plus the locally generated key pair.
    /// </summary>
    [Serializable]
    public class CompleteOtpLoginResult
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

        /// <summary>
        /// Locally generated P256 key pair for Turnkey authentication
        /// </summary>
        public KeyPair keyPair;
    }
}
