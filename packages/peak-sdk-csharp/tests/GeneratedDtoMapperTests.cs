using System;
using FluentAssertions;
using Xunit;
using KyuzanInc.Peak.Sdk.Mapping;
using Gen = KyuzanInc.Peak.PublicApiClient.Model;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class GeneratedDtoMapperTests
    {
        [Fact]
        public void AccountAddress_MapsEnumsToWireStrings_AndKeepsScalars()
        {
            const string json =
                "{\"id\":\"a1\",\"accountId\":\"acc1\",\"address\":\"0xabc\"," +
                "\"chainType\":\"evm\",\"bitcoinAddressType\":\"p2wpkh\"}";
            var dto = PeakResponseJson.Deserialize<Gen.AccountAddressResponseDto>(json)!;

            var pub = dto.ToPublic();

            pub.Id.Should().Be("a1");
            pub.Address.Should().Be("0xabc");
            pub.ChainType.Should().Be("evm");
            pub.BitcoinAddressType.Should().Be("p2wpkh");
        }

        [Fact]
        public void Account_MapsDecimalIndexToInt()
        {
            // The generated DTO marks id/userId/accountSourceId/accountIndex/
            // originProjectId as [DataMember(IsRequired = true)]; Newtonsoft throws
            // if any required field is absent, so fixtures include all of them.
            const string json =
                "{\"id\":\"acc1\",\"userId\":\"u1\",\"accountSourceId\":\"src1\"," +
                "\"accountIndex\":3,\"originProjectId\":\"proj1\"}";
            var dto = PeakResponseJson.Deserialize<Gen.AccountResponseDto>(json)!;

            dto.ToPublic().AccountIndex.Should().Be(3);
        }

        [Fact]
        public void Account_RejectsNonIntegralIndex()
        {
            const string json =
                "{\"id\":\"acc1\",\"userId\":\"u1\",\"accountSourceId\":\"src1\"," +
                "\"accountIndex\":1.5,\"originProjectId\":\"proj1\"}";
            var dto = PeakResponseJson.Deserialize<Gen.AccountResponseDto>(json)!;

            Action act = () => dto.ToPublic();
            act.Should().Throw<PeakError>().Which.Code.Should().Be(PeakErrorCode.InvalidResponse);
        }

        [Fact]
        public void AccountSource_MapsSourceTypeToWireString()
        {
            const string json =
                "{\"id\":\"s1\",\"userId\":\"u1\",\"originProjectId\":\"proj1\"," +
                "\"turnkeyResourceId\":\"tk1\",\"sourceType\":\"private-key\",\"creationMethod\":\"imported\"}";
            var dto = PeakResponseJson.Deserialize<Gen.AccountSourceResponseDto>(json)!;

            var pub = dto.ToPublic();

            pub.SourceType.Should().Be("private-key");
            pub.CreationMethod.Should().Be("imported");
        }

        [Fact]
        public void CompleteOtpLogin_MapsNestedAndList()
        {
            const string json =
                "{\"user\":{\"id\":\"u1\",\"email\":\"a@b.c\",\"originProjectId\":\"proj1\"," +
                "\"turnkeySubOrgId\":\"sub1\",\"turnkeyRootUserId\":\"root1\",\"deletionStatus\":\"none\"}," +
                "\"sessionJwt\":\"jwt\",\"isNewUser\":true," +
                "\"accountAddresses\":[{\"id\":\"ad1\",\"accountId\":\"acc1\",\"address\":\"0xaddr\",\"chainType\":\"solana\"}]}";
            var dto = PeakResponseJson.Deserialize<Gen.CompleteOtpLoginResponseDto>(json)!;

            var pub = dto.ToPublic();

            pub.SessionJwt.Should().Be("jwt");
            pub.IsNewUser.Should().BeTrue();
            pub.User!.Email.Should().Be("a@b.c");
            pub.User!.DeletionStatus.Should().Be("none");
            pub.AccountAddresses.Should().ContainSingle();
            pub.AccountAddresses![0].ChainType.Should().Be("solana");
        }
    }
}
