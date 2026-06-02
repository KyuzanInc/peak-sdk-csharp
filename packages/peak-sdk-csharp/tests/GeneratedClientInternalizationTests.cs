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
            // D13: the embedded client DLL must expose no public surface, so
            // consumers cannot reference generated types even though it ships
            // under lib/{tfm}. GetExportedTypes() returns only public types.
            var assembly = typeof(Gen.InitOtpLoginResponseDto).Assembly;
            assembly.GetExportedTypes().Should().BeEmpty();
        }
    }
}
