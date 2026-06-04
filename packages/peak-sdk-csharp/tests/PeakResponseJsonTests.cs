using FluentAssertions;
using KyuzanInc.Peak.Sdk.Models;
using Xunit;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class PeakResponseJsonTests
    {
        [Fact]
        public void Deserialize_PublicDto_ReadsCamelCaseFields()
        {
            const string json = "{\"otpId\":\"otp-123\"}";
            var dto = PeakResponseJson.Deserialize<InitOtpLoginResponse>(json);
            dto.Should().NotBeNull();
            dto!.OtpId.Should().Be("otp-123");
        }

        [Fact]
        public void Deserialize_PublicDto_ReadsEnumishStringPassthrough()
        {
            const string json =
                "{\"id\":\"a1\",\"accountId\":\"acc1\",\"address\":\"0xabc\",\"chainType\":\"evm\"}";
            var dto = PeakResponseJson.Deserialize<AccountAddressResponse>(json);
            dto.Should().NotBeNull();
            dto!.ChainType.Should().Be("evm");
        }

        [Fact]
        public void Deserialize_UnregisteredType_ThrowsPeakError()
        {
            // The AOT guarantee: a type not registered in PeakJsonContext throws
            // rather than silently falling back to reflection.
            System.Action act = () => PeakResponseJson.Deserialize<UnregisteredProbe>("{}");
            act.Should().Throw<PeakError>().Which.Code.Should().Be(PeakErrorCode.InvalidArgument);
        }

        private sealed class UnregisteredProbe { public string? X { get; set; } }
    }
}
