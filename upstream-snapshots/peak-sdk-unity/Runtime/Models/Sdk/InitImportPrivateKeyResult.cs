using System;

namespace Peak.Models.Sdk
{
    /// <summary>
    /// Result from initializing a private key import.
    /// Contains the encryption bundle and Turnkey credentials needed to encrypt the private key.
    /// </summary>
    [Serializable]
    public class InitImportPrivateKeyResult
    {
        /// <summary>
        /// The import bundle from Turnkey used to encrypt the private key.
        /// </summary>
        public string importBundle;

        /// <summary>
        /// The Turnkey organization ID.
        /// </summary>
        public string organizationId;

        /// <summary>
        /// The Turnkey user ID.
        /// </summary>
        public string userId;
    }
}
