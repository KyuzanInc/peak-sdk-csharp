using System;

namespace Peak.Models.Api
{
    /// <summary>
    /// Entity DTO for account address information.
    /// Mirrors AccountAddressResponseDto in peak-server database response DTOs.
    /// </summary>
    [Serializable]
    public class AccountAddressResponse
    {
        /// <summary>
        /// Account address ID
        /// </summary>
        public string id;

        /// <summary>
        /// Account ID
        /// </summary>
        public string accountId;

        /// <summary>
        /// Blockchain address
        /// </summary>
        public string address;

        /// <summary>
        /// Chain type: "evm", "bitcoin", "solana", "sui"
        /// </summary>
        public string chainType;

        /// <summary>
        /// Bitcoin address type (only for bitcoin chain): "p2pkh", "p2sh", "p2wpkh", "p2wsh", "p2tr"
        /// Nullable - only present for bitcoin addresses.
        /// </summary>
        public string bitcoinAddressType;
    }
}
