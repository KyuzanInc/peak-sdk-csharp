using System;
using Peak.Models.Api;

namespace Peak.Models.Sdk
{
    /// <summary>
    /// Result from completing a private key import.
    /// Contains the created account, address, and source information.
    /// </summary>
    [Serializable]
    public class CompleteImportPrivateKeyResult
    {
        /// <summary>
        /// The created account information.
        /// </summary>
        public AccountResponse account;

        /// <summary>
        /// The created account address.
        /// </summary>
        public AccountAddressResponse accountAddress;

        /// <summary>
        /// The account source information.
        /// </summary>
        public AccountSourceResponse accountSource;
    }
}
