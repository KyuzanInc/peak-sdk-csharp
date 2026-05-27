using System;

namespace Peak.Models.Api
{
    /// <summary>
    /// Response wrapper for GET /public-api/v1/accounts/list endpoint.
    /// </summary>
    [Serializable]
    public class ListAccountsResponse
    {
        /// <summary>
        /// Array of accounts
        /// </summary>
        public AccountResponse[] accounts;
    }

    /// <summary>
    /// Response wrapper for GET /public-api/v1/accounts/list-addresses endpoint.
    /// </summary>
    [Serializable]
    public class ListAccountAddressesResponse
    {
        /// <summary>
        /// Array of account addresses
        /// </summary>
        public AccountAddressResponse[] accountAddresses;
    }

    /// <summary>
    /// Response wrapper for GET /public-api/v1/accounts/get-address-detail endpoint.
    /// </summary>
    [Serializable]
    public class GetAddressDetailResponse
    {
        /// <summary>
        /// Account address information
        /// </summary>
        public AccountAddressResponse accountAddress;

        /// <summary>
        /// Account information
        /// </summary>
        public AccountResponse account;

        /// <summary>
        /// Account source information
        /// </summary>
        public AccountSourceResponse accountSource;
    }
}
