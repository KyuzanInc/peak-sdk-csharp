using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Peak.Models.Api;
using Peak.Utils;

namespace Peak.Services.Authenticated
{
    /// <summary>
    /// Service for account-related operations.
    /// Aligned with AccountService in peak-sdk-browser.
    /// </summary>
    public class AccountService
    {
        private readonly IPeakHttpClient httpClient;
        private readonly string sessionJwt;

        /// <summary>
        /// Creates a new AccountService instance.
        /// </summary>
        /// <param name="apiUrl">The API base URL</param>
        /// <param name="projectApiKey">The project API key</param>
        /// <param name="sessionJwt">The session JWT for authentication</param>
        /// <param name="httpClient">Optional HTTP client for testing</param>
        public AccountService(string apiUrl, string projectApiKey, string sessionJwt, IPeakHttpClient httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(sessionJwt))
            {
                throw new ArgumentException("Session JWT is required", nameof(sessionJwt));
            }

            this.httpClient = httpClient ?? new PeakHttpClient(apiUrl, projectApiKey);
            this.sessionJwt = sessionJwt;
        }

        private Dictionary<string, string> CreateAuthHeaders()
        {
            return new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {sessionJwt}"
            };
        }

        /// <summary>
        /// List all accounts for the authenticated user.
        /// Aligned with listAccounts() in peak-sdk-browser.
        /// </summary>
        /// <returns>Array of account information</returns>
        /// <remarks>
        /// Note: Addresses are NOT included in the response for performance reasons.
        /// Use ListAccountAddressesAsync() to get addresses for a specific account.
        /// </remarks>
        public async UniTask<AccountResponse[]> ListAccountsAsync()
        {
            var response = await httpClient.GetAsync<ListAccountsResponse>(
                "public-api/v1/accounts/list",
                CreateAuthHeaders());
            return response?.accounts ?? Array.Empty<AccountResponse>();
        }

        /// <summary>
        /// List all addresses for a specific account.
        /// Aligned with listAccountAddresses() in peak-sdk-browser.
        /// </summary>
        /// <param name="accountId">The account ID to list addresses for</param>
        /// <returns>Array of account address information</returns>
        public async UniTask<AccountAddressResponse[]> ListAccountAddressesAsync(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new ArgumentException("Account ID is required", nameof(accountId));
            }

            var response = await httpClient.GetAsync<ListAccountAddressesResponse>(
                $"public-api/v1/accounts/list-addresses?accountId={Uri.EscapeDataString(accountId)}",
                CreateAuthHeaders());
            return response?.accountAddresses ?? Array.Empty<AccountAddressResponse>();
        }

        /// <summary>
        /// Update the display name of an account.
        /// Aligned with updateAccountDisplayName() in peak-sdk-browser.
        /// </summary>
        /// <param name="accountId">The account ID to update</param>
        /// <param name="displayName">The new display name</param>
        /// <returns>The updated account information</returns>
        public async UniTask<AccountResponse> UpdateAccountDisplayNameAsync(string accountId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new ArgumentException("Account ID is required", nameof(accountId));
            }

            var request = new UpdateAccountDisplayNameRequest(accountId, displayName);

            return await httpClient.PostAsync<AccountResponse>(
                "public-api/v1/accounts/update-display-name",
                request,
                CreateAuthHeaders());
        }

    }
}
