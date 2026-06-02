using System.IO;
using System.Runtime.CompilerServices;
using FluentAssertions;
using PublicApiGenerator;
using Xunit;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class PublicSurfaceBaselineTests
    {
        [Fact]
        public void PublicSurface_HasNoGeneratedTypes_AndMatchesBaseline()
        {
            var api = typeof(PeakClient).Assembly
                .GeneratePublicApi()
                .Replace("\r\n", "\n");

            // D13: no generated type may appear on the SDK public surface.
            api.Should().NotContain("KyuzanInc.Peak.PublicApiClient");

            var approvedPath = ApprovedPath();
            var receivedPath = approvedPath.Replace(".approved.", ".received.");

            // Never auto-approve: a missing or empty baseline must FAIL so CI
            // cannot silently accept surface drift. Write the received file for
            // the engineer to inspect and promote (Step 2).
            if (!File.Exists(approvedPath) || new FileInfo(approvedPath).Length == 0)
            {
                File.WriteAllText(receivedPath, api);
                Assert.Fail($"Approved baseline missing/empty. Review '{receivedPath}' and copy it to '{approvedPath}'.");
            }

            var approved = File.ReadAllText(approvedPath).Replace("\r\n", "\n");
            if (api != approved)
            {
                File.WriteAllText(receivedPath, api); // for inspection on drift
            }
            api.Should().Be(approved);
        }

        private static string ApprovedPath([CallerFilePath] string thisFile = "") =>
            Path.Combine(Path.GetDirectoryName(thisFile)!, "PublicApi.Sdk.approved.txt");
    }
}
