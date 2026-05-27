using System.Threading.Tasks;
using FluentAssertions;
using KyuzanInc.Peak.Sdk.Storage;
using Xunit;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class InMemoryStorageTests
    {
        [Fact]
        public void Get_ReturnsNull_WhenMissing()
        {
            var s = new InMemoryStorage();
            s.Get("nope").Should().BeNull();
        }

        [Fact]
        public void SetGet_RoundTrip()
        {
            var s = new InMemoryStorage();
            s.Set("k", "v");
            s.Get("k").Should().Be("v");
        }

        [Fact]
        public void Delete_RemovesValue()
        {
            var s = new InMemoryStorage();
            s.Set("k", "v");
            s.Delete("k");
            s.Get("k").Should().BeNull();
        }

        [Fact]
        public async Task AsyncOverloads_DelegateToSync()
        {
            var s = new InMemoryStorage();
            await s.SetAsync("k", "v");
            (await s.GetAsync("k")).Should().Be("v");
            await s.DeleteAsync("k");
            (await s.GetAsync("k")).Should().BeNull();
        }
    }
}
