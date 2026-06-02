using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KyuzanInc.Peak.Sdk.Models;
using KyuzanInc.Peak.Sdk.Services;
using KyuzanInc.Peak.Sdk.Utils;
using NSubstitute;
using Xunit;
using Gen = KyuzanInc.Peak.PublicApiClient.Model;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class AccountServiceTests
    {
        [Fact]
        public async Task ListAccounts_MapsGeneratedDtos()
        {
            var http = Substitute.For<IPeakHttpClient>();
            // Generated AccountResponseDto requires id/userId/accountSourceId/
            // accountIndex/originProjectId (Newtonsoft throws on a missing required
            // field), so the nested fixture supplies all of them.
            var dto = PeakResponseJson.Deserialize<Gen.ListAccountsResponseDto>(
                "{\"accounts\":[{\"id\":\"acc1\",\"userId\":\"u1\",\"accountSourceId\":\"src1\"," +
                "\"accountIndex\":2,\"originProjectId\":\"proj1\"}]}")!;
            http.GetAsync<Gen.ListAccountsResponseDto>(
                    "public-api/v1/accounts/list", Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(dto);

            var svc = new AccountService("https://api.peak.xyz", "k", "jwt", http);
            var accounts = await svc.ListAccountsAsync();

            accounts.Should().ContainSingle();
            accounts[0].Id.Should().Be("acc1");
            accounts[0].AccountIndex.Should().Be(2);
        }

        [Fact]
        public async Task UpdateDisplayName_MapsNestedAccount()
        {
            var http = Substitute.For<IPeakHttpClient>();
            var dto = PeakResponseJson.Deserialize<Gen.UpdateAccountDisplayNameResponseDto>(
                "{\"account\":{\"id\":\"acc1\",\"userId\":\"u1\",\"accountSourceId\":\"src1\"," +
                "\"accountIndex\":0,\"originProjectId\":\"proj1\",\"displayName\":\"new name\"}}")!;
            http.PostAsync<UpdateAccountDisplayNameRequest, Gen.UpdateAccountDisplayNameResponseDto>(
                    "public-api/v1/accounts/update-display-name",
                    Arg.Any<UpdateAccountDisplayNameRequest>(),
                    Arg.Any<IReadOnlyDictionary<string, string>>(),
                    Arg.Any<CancellationToken>())
                .Returns(dto);

            var svc = new AccountService("https://api.peak.xyz", "k", "jwt", http);
            var account = await svc.UpdateAccountDisplayNameAsync("acc1", "new name");

            account!.Id.Should().Be("acc1");
            account.DisplayName.Should().Be("new name");
        }
    }
}
