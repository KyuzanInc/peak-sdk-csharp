using FluentAssertions;
using Xunit;
using Gen = KyuzanInc.Peak.PublicApiClient.Model;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class GeneratedClientInternalizationTests
    {
        [Fact]
        public void GeneratedClientAssembly_ExportsNoPublicTypes()
        {
            // D13: the generated client is build-time/test-only — since issue #18
            // it is no longer embedded in or shipped with the SDK package. This
            // test guards that the internalize step keeps the generated assembly's
            // exported surface empty, so a future regeneration cannot silently
            // re-expose generated types. GetExportedTypes() returns only public types.
            var assembly = typeof(Gen.InitOtpLoginResponseDto).Assembly;
            assembly.GetExportedTypes().Should().BeEmpty();
        }
    }
}
