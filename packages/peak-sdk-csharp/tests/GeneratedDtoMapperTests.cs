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

        // --- forward compatibility: unknown server enum values ---
        //
        // The peak-server spec is synced from an independently-evolving monorepo,
        // so it can add enum values the client does not know yet. An unknown value
        // must NOT hard-fail the whole response (the pre-generated-client behavior
        // was string passthrough); it maps to null on the public string? field and
        // every other field still flows through.

        [Fact]
        public void AccountAddress_UnknownChainType_DoesNotThrow_AndKeepsScalars()
        {
            // "aptos" is a plausible future chainType not in the pinned spec.
            const string json =
                "{\"id\":\"a1\",\"accountId\":\"acc1\",\"address\":\"0xabc\"," +
                "\"chainType\":\"aptos\"}";

            var dto = PeakResponseJson.Deserialize<Gen.AccountAddressResponseDto>(json)!;
            var pub = dto.ToPublic();

            pub.Id.Should().Be("a1");
            pub.AccountId.Should().Be("acc1");
            pub.Address.Should().Be("0xabc");
            pub.ChainType.Should().BeNull();
        }

        [Fact]
        public void AccountAddress_UnknownBitcoinAddressType_MapsToNull_AndKeepsKnownChainType()
        {
            const string json =
                "{\"id\":\"a1\",\"accountId\":\"acc1\",\"address\":\"bc1future\"," +
                "\"chainType\":\"bitcoin\",\"bitcoinAddressType\":\"p2f?\"}";

            var dto = PeakResponseJson.Deserialize<Gen.AccountAddressResponseDto>(json)!;
            var pub = dto.ToPublic();

            pub.ChainType.Should().Be("bitcoin");
            pub.BitcoinAddressType.Should().BeNull();
        }

        [Fact]
        public void AccountSource_UnknownSourceTypeAndCreationMethod_MapToNull()
        {
            const string json =
                "{\"id\":\"s1\",\"userId\":\"u1\",\"originProjectId\":\"proj1\"," +
                "\"turnkeyResourceId\":\"tk1\",\"sourceType\":\"hardware-wallet\",\"creationMethod\":\"teleported\"}";

            var dto = PeakResponseJson.Deserialize<Gen.AccountSourceResponseDto>(json)!;
            var pub = dto.ToPublic();

            pub.Id.Should().Be("s1");
            pub.TurnkeyResourceId.Should().Be("tk1");
            pub.SourceType.Should().BeNull();
            pub.CreationMethod.Should().BeNull();
        }

        [Fact]
        public void CompleteOtpLogin_UnknownNestedEnum_DoesNotRejectWholeResponse()
        {
            // The regression this guards: one unknown chainType nested in an
            // otherwise-valid login response previously failed the entire call as
            // InvalidResponse. Now the rest of the payload still maps.
            const string json =
                "{\"user\":{\"id\":\"u1\",\"email\":\"a@b.c\",\"originProjectId\":\"proj1\"," +
                "\"turnkeySubOrgId\":\"sub1\",\"turnkeyRootUserId\":\"root1\",\"deletionStatus\":\"frozen\"}," +
                "\"sessionJwt\":\"jwt\",\"isNewUser\":true," +
                "\"accountAddresses\":[{\"id\":\"ad1\",\"accountId\":\"acc1\",\"address\":\"0xaddr\",\"chainType\":\"aptos\"}]}";

            var dto = PeakResponseJson.Deserialize<Gen.CompleteOtpLoginResponseDto>(json)!;
            var pub = dto.ToPublic();

            pub.SessionJwt.Should().Be("jwt");
            pub.IsNewUser.Should().BeTrue();
            pub.User!.Email.Should().Be("a@b.c");
            pub.User!.DeletionStatus.Should().BeNull();
            pub.AccountAddresses.Should().ContainSingle();
            pub.AccountAddresses![0].Address.Should().Be("0xaddr");
            pub.AccountAddresses![0].ChainType.Should().BeNull();
        }

        [Fact]
        public void GetAddressDetail_MapsAllThreeNestedEntities()
        {
            const string json =
                "{\"accountAddress\":{\"id\":\"ad1\",\"accountId\":\"acc1\",\"address\":\"0xabc\",\"chainType\":\"evm\"}," +
                "\"account\":{\"id\":\"acc1\",\"userId\":\"u1\",\"accountSourceId\":\"src1\",\"accountIndex\":0,\"originProjectId\":\"proj1\"}," +
                "\"accountSource\":{\"id\":\"s1\",\"userId\":\"u1\",\"originProjectId\":\"proj1\",\"turnkeyResourceId\":\"tk1\",\"sourceType\":\"private-key\",\"creationMethod\":\"imported\"}}";
            var dto = PeakResponseJson.Deserialize<Gen.GetAccountAddressWithAccountAndSourceResponseDto>(json)!;

            var pub = dto.ToPublic();

            pub.AccountAddress!.Address.Should().Be("0xabc");
            pub.Account!.Id.Should().Be("acc1");
            pub.AccountSource!.SourceType.Should().Be("private-key");
        }

        [Fact]
        public void AccountAddress_NumericChainType_MapsToNull_NotCoerced()
        {
            // A numeric enum token must NOT be coerced to the member with that
            // ordinal (chainType 1 would otherwise read as ChainTypeEnum.Evm ->
            // "evm"). It is unrecognized, so the public field is null.
            const string json =
                "{\"id\":\"a1\",\"accountId\":\"acc1\",\"address\":\"0xabc\",\"chainType\":1}";
            var dto = PeakResponseJson.Deserialize<Gen.AccountAddressResponseDto>(json)!;

            dto.ToPublic().ChainType.Should().BeNull();
        }
    }
}
