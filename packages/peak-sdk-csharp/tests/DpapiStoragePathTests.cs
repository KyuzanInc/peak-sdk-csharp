using System;
using System.IO;
using FluentAssertions;
using KyuzanInc.Peak.Sdk.Storage;
using Xunit;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class DpapiStoragePathTests
    {
        [Theory]
        [InlineData(".")]
        [InlineData("..")]
        public void ResolveBaseDirectory_RejectsTraversalNamespace(string @namespace)
        {
            Action act = () => DpapiStoragePath.ResolveBaseDirectory(
                baseDirectory: null,
                @namespace,
                localApplicationDataDirectory: Path.GetTempPath());

            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData("default")]
        [InlineData("Tenant_01")]
        [InlineData("tenant.v1")]
        public void ResolveBaseDirectory_DefaultPathIsStrictlyContainedBySdkRoot(string @namespace)
        {
            var localApplicationData = Path.Combine(Path.GetTempPath(), "peak-sdk-path-tests");
            var root = Path.GetFullPath(Path.Combine(localApplicationData, "KyuzanInc", "PeakSdk"));

            var resolved = DpapiStoragePath.ResolveBaseDirectory(
                baseDirectory: null,
                @namespace,
                localApplicationData);

            Path.GetRelativePath(root, resolved).Should().Be(@namespace);
            resolved.Should().StartWith(root + Path.DirectorySeparatorChar);
        }

        [Fact]
        public void ResolveBaseDirectory_ExplicitDirectoryIsNotChangedOrNamespaced()
        {
            var explicitDirectory = Path.Combine("relative", "consumer-selected-path");

            var resolved = DpapiStoragePath.ResolveBaseDirectory(
                explicitDirectory,
                "tenant.v1",
                localApplicationDataDirectory: Path.GetTempPath());

            resolved.Should().Be(explicitDirectory);
        }
    }
}
