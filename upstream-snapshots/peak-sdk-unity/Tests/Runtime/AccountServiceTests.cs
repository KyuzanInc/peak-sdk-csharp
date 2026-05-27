using System;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using Peak.Models.Api;
using Peak.Services.Authenticated;
using Peak.Utils;

namespace Peak.Tests
{
    public class AccountServiceTests
    {
        private const string ApiUrl = "https://api.test";
        private const string ProjectApiKey = "test-api-key";
        private const string SessionJwt = "session-jwt";

        private IPeakHttpClient httpClient;
        private AccountService accountService;

        [SetUp]
        public void SetUp()
        {
            httpClient = Substitute.For<IPeakHttpClient>();
            accountService = new AccountService(ApiUrl, ProjectApiKey, SessionJwt, httpClient);
        }

        [Test]
        public void Constructor_WithNullSessionJwt_ThrowsArgumentException()
        {
            var localHttpClient = Substitute.For<IPeakHttpClient>();
            Assert.Throws<ArgumentException>(
                () => new AccountService(ApiUrl, ProjectApiKey, null, localHttpClient));
        }

        [Test]
        public void Constructor_WithEmptySessionJwt_ThrowsArgumentException()
        {
            var localHttpClient = Substitute.For<IPeakHttpClient>();
            Assert.Throws<ArgumentException>(
                () => new AccountService(ApiUrl, ProjectApiKey, string.Empty, localHttpClient));
        }

        [Test]
        public async System.Threading.Tasks.Task ListAccountsAsync_ReturnsAccountsArray()
        {
            var expected = new ListAccountsResponse
            {
                accounts = new[]
                {
                    new AccountResponse { id = "account-1", userId = "user-1" },
                    new AccountResponse { id = "account-2", userId = "user-1" },
                }
            };

            httpClient
                .GetAsync<ListAccountsResponse>(
                    "public-api/v1/accounts/list",
                    Arg.Any<System.Collections.Generic.Dictionary<string, string>>())
                .Returns(UniTask.FromResult(expected));

            var result = await accountService.ListAccountsAsync();

            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("account-1", result[0].id);
            Assert.AreEqual("account-2", result[1].id);
        }

        [Test]
        public async System.Threading.Tasks.Task ListAccountsAsync_WhenResponseIsNull_ReturnsEmptyArray()
        {
            httpClient
                .GetAsync<ListAccountsResponse>(
                    "public-api/v1/accounts/list",
                    Arg.Any<System.Collections.Generic.Dictionary<string, string>>())
                .Returns(UniTask.FromResult<ListAccountsResponse>(null));

            var result = await accountService.ListAccountsAsync();

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public async System.Threading.Tasks.Task ListAccountsAsync_WhenAccountsFieldIsNull_ReturnsEmptyArray()
        {
            httpClient
                .GetAsync<ListAccountsResponse>(
                    "public-api/v1/accounts/list",
                    Arg.Any<System.Collections.Generic.Dictionary<string, string>>())
                .Returns(UniTask.FromResult(new ListAccountsResponse { accounts = null }));

            var result = await accountService.ListAccountsAsync();

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public async System.Threading.Tasks.Task ListAccountAddressesAsync_ReturnsAddressesArray()
        {
            const string accountId = "account-1";
            var expected = new ListAccountAddressesResponse
            {
                accountAddresses = new[]
                {
                    new AccountAddressResponse { id = "addr-1", accountId = accountId, address = "0xabc", chainType = "evm" },
                    new AccountAddressResponse { id = "addr-2", accountId = accountId, address = "Sol123", chainType = "solana" },
                }
            };

            httpClient
                .GetAsync<ListAccountAddressesResponse>(
                    $"public-api/v1/accounts/list-addresses?accountId={Uri.EscapeDataString(accountId)}",
                    Arg.Any<System.Collections.Generic.Dictionary<string, string>>())
                .Returns(UniTask.FromResult(expected));

            var result = await accountService.ListAccountAddressesAsync(accountId);

            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("evm", result[0].chainType);
            Assert.AreEqual("solana", result[1].chainType);
        }

        [Test]
        public void ListAccountAddressesAsync_WithNullAccountId_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(
                async () => await accountService.ListAccountAddressesAsync(null));
        }

        [Test]
        public void ListAccountAddressesAsync_WithEmptyAccountId_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(
                async () => await accountService.ListAccountAddressesAsync(string.Empty));
        }

        [Test]
        public async System.Threading.Tasks.Task UpdateAccountDisplayNameAsync_SendsRequestAndReturnsAccount()
        {
            const string accountId = "account-1";
            const string newDisplayName = "Main Wallet";
            UpdateAccountDisplayNameRequest capturedRequest = null;

            var expected = new AccountResponse
            {
                id = accountId,
                userId = "user-1",
                displayName = newDisplayName,
            };

            httpClient
                .PostAsync<AccountResponse>(
                    "public-api/v1/accounts/update-display-name",
                    Arg.Do<object>(payload => capturedRequest = payload as UpdateAccountDisplayNameRequest),
                    Arg.Any<System.Collections.Generic.Dictionary<string, string>>())
                .Returns(UniTask.FromResult(expected));

            var result = await accountService.UpdateAccountDisplayNameAsync(accountId, newDisplayName);

            Assert.IsNotNull(capturedRequest);
            Assert.AreEqual(accountId, capturedRequest.accountId);
            Assert.AreEqual(newDisplayName, capturedRequest.displayName);

            Assert.AreEqual(accountId, result.id);
            Assert.AreEqual(newDisplayName, result.displayName);
        }

        [Test]
        public void UpdateAccountDisplayNameAsync_WithNullAccountId_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(
                async () => await accountService.UpdateAccountDisplayNameAsync(null, "Main Wallet"));
        }

        [Test]
        public void UpdateAccountDisplayNameAsync_WithEmptyAccountId_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentException>(
                async () => await accountService.UpdateAccountDisplayNameAsync(string.Empty, "Main Wallet"));
        }
    }
}
