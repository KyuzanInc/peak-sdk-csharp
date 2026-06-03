using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KyuzanInc.Peak.Sdk.Models;
using KyuzanInc.Peak.Sdk.Services;
using KyuzanInc.Peak.Sdk.Utils;
using NSubstitute;
using Xunit;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class AccountServiceTests
    {
        [Fact]
        public async Task ListAccounts_ReturnsPublicDtos()
        {
            var http = Substitute.For<IPeakHttpClient>();
            http.GetAsync<ListAccountsResponse>(
                    "public-api/v1/accounts/list", Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(new ListAccountsResponse
                {
                    Accounts = new[] { new AccountResponse { Id = "acc1", AccountIndex = 2 } },
                });

            var svc = new AccountService("https://api.peak.xyz", "k", "jwt", http);
            var accounts = await svc.ListAccountsAsync();

            accounts.Should().ContainSingle();
            accounts[0].Id.Should().Be("acc1");
            accounts[0].AccountIndex.Should().Be(2);
        }

        [Fact]
        public async Task UpdateDisplayName_ReturnsNestedAccount()
        {
            var http = Substitute.For<IPeakHttpClient>();
            http.PostAsync<UpdateAccountDisplayNameRequest, UpdateAccountDisplayNameEnvelope>(
                    "public-api/v1/accounts/update-display-name",
                    Arg.Any<UpdateAccountDisplayNameRequest>(),
                    Arg.Any<IReadOnlyDictionary<string, string>>(),
                    Arg.Any<CancellationToken>())
                .Returns(new UpdateAccountDisplayNameEnvelope
                {
                    Account = new AccountResponse { Id = "acc1", DisplayName = "new name" },
                });

            var svc = new AccountService("https://api.peak.xyz", "k", "jwt", http);
            var account = await svc.UpdateAccountDisplayNameAsync("acc1", "new name");

            account!.Id.Should().Be("acc1");
            account.DisplayName.Should().Be("new name");
        }

        [Fact]
        public async Task ListAccountAddresses_ReturnsPublicDtos()
        {
            var http = Substitute.For<IPeakHttpClient>();
            http.GetAsync<ListAccountAddressesResponse>(
                    Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(new ListAccountAddressesResponse
                {
                    AccountAddresses = new[] { new AccountAddressResponse { Id = "ad1", Address = "0xabc", ChainType = "evm" } },
                });

            var svc = new AccountService("https://api.peak.xyz", "k", "jwt", http);
            var addresses = await svc.ListAccountAddressesAsync("acc1");

            addresses.Should().ContainSingle();
            addresses[0].Address.Should().Be("0xabc");
            addresses[0].ChainType.Should().Be("evm");
        }

        [Fact]
        public async Task ListAccounts_NullResponse_ReturnsEmptyArray()
        {
            // Unconfigured NSubstitute returns Task.FromResult(default) -> null DTO.
            var http = Substitute.For<IPeakHttpClient>();
            var svc = new AccountService("https://api.peak.xyz", "k", "jwt", http);

            var accounts = await svc.ListAccountsAsync();

            accounts.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public async Task ListAccountAddresses_NullResponse_ReturnsEmptyArray()
        {
            var http = Substitute.For<IPeakHttpClient>();
            var svc = new AccountService("https://api.peak.xyz", "k", "jwt", http);

            var addresses = await svc.ListAccountAddressesAsync("acc1");

            addresses.Should().NotBeNull().And.BeEmpty();
        }
    }
}
