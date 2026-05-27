using System;
using FluentAssertions;
using Xunit;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class PeakErrorTests
    {
        [Fact]
        public void From_PreservesExistingPeakError()
        {
            var original = new PeakError(PeakErrorCode.HttpError, "boom");
            PeakError.From(original).Should().BeSameAs(original);
        }

        [Fact]
        public void From_WrapsArbitraryException()
        {
            var ex = new InvalidOperationException("inner");
            var wrapped = PeakError.From(ex, PeakErrorCode.NetworkError, "wrapped");
            wrapped.Code.Should().Be(PeakErrorCode.NetworkError);
            wrapped.Message.Should().Be("wrapped");
            wrapped.InnerException.Should().BeSameAs(ex);
        }

        [Fact]
        public void IsAny_ChecksTypeOnly()
        {
            PeakError.IsAny(new PeakError(PeakErrorCode.Unknown, "x")).Should().BeTrue();
            PeakError.IsAny(new InvalidOperationException()).Should().BeFalse();
            PeakError.IsAny(null).Should().BeFalse();
        }

        [Fact]
        public void Code_DefaultsToUnknown_WhenNull()
        {
            var err = new PeakError(null!, "x");
            err.Code.Should().Be(PeakErrorCode.Unknown);
        }
    }
}
