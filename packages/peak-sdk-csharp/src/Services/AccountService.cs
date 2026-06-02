// Ported from upstream-snapshots/peak-sdk-unity/Runtime/Services/Authenticated/AccountService.cs.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KyuzanInc.Peak.Sdk.Models;
using KyuzanInc.Peak.Sdk.Utils;
using KyuzanInc.Peak.Sdk.Mapping;
using Gen = KyuzanInc.Peak.PublicApiClient.Model;

namespace KyuzanInc.Peak.Sdk.Services
{
    public sealed class AccountService
    {
        private readonly IPeakHttpClient httpClient;
        private readonly string sessionJwt;

        public AccountService(string apiUrl, string projectApiKey, string sessionJwt, IPeakHttpClient? httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(sessionJwt))
            {
                throw new PeakError(PeakErrorCode.InvalidArgument, "Session JWT is required");
            }
            this.httpClient = httpClient ?? new DefaultPeakHttpClient(apiUrl, projectApiKey);
            this.sessionJwt = sessionJwt;
        }

        private Dictionary<string, string> CreateAuthHeaders() =>
            new() { ["Authorization"] = $"Bearer {sessionJwt}" };

        public async Task<AccountResponse[]> ListAccountsAsync(CancellationToken cancellationToken = default)
        {
            var dto = await httpClient.GetAsync<Gen.ListAccountsResponseDto>(
                "public-api/v1/accounts/list", CreateAuthHeaders(), cancellationToken).ConfigureAwait(false);
            return dto?.ToPublic().Accounts ?? Array.Empty<AccountResponse>();
        }

        public async Task<AccountAddressResponse[]> ListAccountAddressesAsync(string accountId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new PeakError(PeakErrorCode.InvalidArgument, "Account ID is required");
            }
            var dto = await httpClient.GetAsync<Gen.ListAccountAddressesResponseDto>(
                $"public-api/v1/accounts/list-addresses?accountId={Uri.EscapeDataString(accountId)}",
                CreateAuthHeaders(),
                cancellationToken).ConfigureAwait(false);
            return dto?.ToPublic().AccountAddresses ?? Array.Empty<AccountAddressResponse>();
        }

        public async Task<AccountResponse?> UpdateAccountDisplayNameAsync(string accountId, string displayName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new PeakError(PeakErrorCode.InvalidArgument, "Account ID is required");
            }
            var payload = new UpdateAccountDisplayNameRequest { AccountId = accountId, DisplayName = displayName };
            var dto = await httpClient.PostAsync<UpdateAccountDisplayNameRequest, Gen.UpdateAccountDisplayNameResponseDto>(
                "public-api/v1/accounts/update-display-name",
                payload,
                CreateAuthHeaders(),
                cancellationToken).ConfigureAwait(false);
            // The server wraps the account ({ "account": {...} }); map the nested DTO.
            return dto?.Account?.ToPublic();
        }

        internal async Task<GetAddressDetailResponse?> GetAddressDetailAsync(string address, CancellationToken cancellationToken = default)
        {
            var encoded = Uri.EscapeDataString(address);
            var dto = await httpClient.GetAsync<Gen.GetAccountAddressWithAccountAndSourceResponseDto>(
                $"public-api/v1/accounts/get-address-detail?address={encoded}",
                CreateAuthHeaders(),
                cancellationToken).ConfigureAwait(false);
            return dto?.ToPublic();
        }
    }
}
