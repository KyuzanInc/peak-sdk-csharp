using FluentAssertions;
using Xunit;
using Gen = KyuzanInc.Peak.PublicApiClient.Model;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class PeakResponseJsonTests
    {
        [Fact]
        public void Deserialize_GeneratedDto_ReadsCamelCaseFields()
        {
            const string json = "{\"otpId\":\"otp-123\"}";
            var dto = PeakResponseJson.Deserialize<Gen.InitOtpLoginResponseDto>(json);
            dto.Should().NotBeNull();
            dto!.OtpId.Should().Be("otp-123");
        }

        [Fact]
        public void Deserialize_GeneratedDto_ParsesStringEnums()
        {
            const string json =
                "{\"id\":\"a1\",\"accountId\":\"acc1\",\"address\":\"0xabc\",\"chainType\":\"evm\"}";
            var dto = PeakResponseJson.Deserialize<Gen.AccountAddressResponseDto>(json);
            dto.Should().NotBeNull();
            dto!.ChainType.Should().Be(Gen.ChainTypeEnum.Evm);
        }
    }
}
