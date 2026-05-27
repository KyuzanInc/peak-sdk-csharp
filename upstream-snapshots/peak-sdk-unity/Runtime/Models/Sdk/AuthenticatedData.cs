using System;
using Peak.Utils;

namespace Peak.Models.Sdk
{
    /// <summary>
    /// Contains authentication data for the current session.
    /// Aligned with getAuthenticatedData() in peak-sdk-browser and peak-sdk-node.
    /// </summary>
    [Serializable]
    public class AuthenticatedData
    {
        /// <summary>
        /// The email address of the authenticated user.
        /// </summary>
        public string email;

        /// <summary>
        /// The session JWT token.
        /// </summary>
        public string sessionJwt;

        /// <summary>
        /// The decoded session JWT payload containing Turnkey credentials.
        /// </summary>
        public SessionJwtPayload sessionJwtPayload;
    }
}
