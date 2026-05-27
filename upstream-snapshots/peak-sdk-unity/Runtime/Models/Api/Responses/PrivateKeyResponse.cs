using System;

namespace Peak.Models.Api
{
    /// <summary>
    /// Response DTO for POST /public-api/v1/private-keys/init-import endpoint.
    /// </summary>
    [Serializable]
    public class InitImportPrivateKeyResponse
    {
        /// <summary>
        /// Import bundle from Turnkey
        /// </summary>
        public string importBundle;
    }

    /// <summary>
    /// Response DTO for POST /public-api/v1/private-keys/complete-import endpoint.
    /// </summary>
    [Serializable]
    public class CompleteImportPrivateKeyResponse
    {
        /// <summary>
        /// The created account information
        /// </summary>
        public AccountResponse account;

        /// <summary>
        /// The created account address
        /// </summary>
        public AccountAddressResponse accountAddress;

        /// <summary>
        /// The account source information
        /// </summary>
        public AccountSourceResponse accountSource;
    }

    /// <summary>
    /// Response DTO for POST /public-api/v1/private-keys/export endpoint.
    /// </summary>
    [Serializable]
    public class ExportPrivateKeyResponse
    {
        /// <summary>
        /// Export bundle from Turnkey
        /// </summary>
        public string exportBundle;
    }
}
