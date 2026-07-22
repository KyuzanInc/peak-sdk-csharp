using System;
using System.Threading.Tasks;
using FluentAssertions;
using KyuzanInc.Peak.Sdk.Storage;
using KyuzanInc.Peak.Sdk.Utils;
using NSubstitute;
using Xunit;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class PeakClientTests
    {
        [Fact]
        public void Initialize_RejectsEmptyApiKey()
        {
            Action act = () => PeakClient.Initialize(new PeakClientOptions { ApiUrl = "https://api.peak.xyz", ProjectApiKey = "" });
            act.Should().Throw<PeakError>().Which.Code.Should().Be(PeakErrorCode.InitializationFailed);
        }

        [Fact]
        public void Initialize_RejectsEmptyApiUrl()
        {
            Action act = () => PeakClient.Initialize(new PeakClientOptions { ApiUrl = "", ProjectApiKey = "k" });
            act.Should().Throw<PeakError>().Which.Code.Should().Be(PeakErrorCode.InitializationFailed);
        }

        [Theory]
        [InlineData("http://api.peak.xyz")]
        [InlineData("api.peak.xyz")]
        [InlineData("/relative")]
        [InlineData("https://api.peak.xyz/#fragment")]
        public void Initialize_RejectsUnsafeApiUrl(string apiUrl)
        {
            Action act = () => PeakClient.Initialize(new PeakClientOptions
            {
                ApiUrl = apiUrl,
                ProjectApiKey = "k",
            });

            act.Should().Throw<PeakError>()
                .Which.Code.Should().Be(PeakErrorCode.InitializationFailed);
        }

        [Fact]
        public void Initialize_RejectsApiUrlWithUserInfo()
        {
            var apiUrl = new UriBuilder(Uri.UriSchemeHttps, "api.peak.xyz")
            {
                UserName = "sdk-user",
            }.Uri.AbsoluteUri;
            Action act = () => PeakClient.Initialize(new PeakClientOptions
            {
                ApiUrl = apiUrl,
                ProjectApiKey = "k",
            });

            act.Should().Throw<PeakError>()
                .Which.Code.Should().Be(PeakErrorCode.InitializationFailed);
        }

        [Fact]
        public void Initialize_RejectsUnsafeApiUrl_WhenCustomTransportIsInjected()
        {
            var transport = Substitute.For<IPeakHttpClient>();
            Action act = () => PeakClient.Initialize(new PeakClientOptions
            {
                ApiUrl = "http://api.peak.xyz",
                ProjectApiKey = "k",
                HttpClient = transport,
            });

            act.Should().Throw<PeakError>()
                .Which.Code.Should().Be(PeakErrorCode.InitializationFailed);
        }

        [Fact]
        public void Initialize_AcceptsCustomTransportWithHttpsApiUrl()
        {
            var transport = Substitute.For<IPeakHttpClient>();

            var client = PeakClient.Initialize(new PeakClientOptions
            {
                ApiUrl = "https://api.peak.xyz",
                ProjectApiKey = "k",
                HttpClient = transport,
            });

            client.Should().NotBeNull();
        }

        [Fact]
        public void Initialize_AcceptsTwoArgOverload()
        {
            var client = PeakClient.Initialize("https://api.peak.xyz", "k");
            client.Should().NotBeNull();
        }

        [Fact]
        public void Logout_DeletesSessionData()
        {
            var storage = new InMemoryStorage();
            storage.Set(SessionData.StorageKey, "{}");
            var client = PeakClient.Initialize(new PeakClientOptions
            {
                ApiUrl = "https://api.peak.xyz",
                ProjectApiKey = "k",
                Storage = storage,
            });
            client.Logout();
            storage.Get(SessionData.StorageKey).Should().BeNull();
        }

        [Fact]
        public void Authenticate_ThrowsNotAuthenticated_WhenNothingStored()
        {
            var client = PeakClient.Initialize(new PeakClientOptions
            {
                ApiUrl = "https://api.peak.xyz",
                ProjectApiKey = "k",
                Storage = new InMemoryStorage(),
            });
            Action act = () => client.Authenticate();
            act.Should().Throw<PeakError>().Which.Code.Should().Be(PeakErrorCode.NotAuthenticated);
        }

        [Fact]
        public async Task AuthenticateAsync_ThrowsNotAuthenticated_WhenNothingStored()
        {
            var client = PeakClient.Initialize(new PeakClientOptions
            {
                ApiUrl = "https://api.peak.xyz",
                ProjectApiKey = "k",
                Storage = new InMemoryStorage(),
            });
            Func<Task> act = async () => await client.AuthenticateAsync();
            await act.Should().ThrowAsync<PeakError>();
        }

        [Fact]
        public void Authenticate_ThrowsNotAuthenticated_AndClearsStorage_WhenSessionIncomplete()
        {
            var storage = new InMemoryStorage();
            storage.Set(SessionData.StorageKey, "{\"email\":\"a\"}");
            var client = PeakClient.Initialize(new PeakClientOptions
            {
                ApiUrl = "https://api.peak.xyz",
                ProjectApiKey = "k",
                Storage = storage,
            });
            Action act = () => client.Authenticate();
            act.Should().Throw<PeakError>().Which.Code.Should().Be(PeakErrorCode.NotAuthenticated);
            storage.Get(SessionData.StorageKey).Should().BeNull("session is cleared on invalid storage");
        }

        [Fact]
        public void Authenticate_ThrowsSessionStorageMissing_AndClearsStorage_OnBadJson()
        {
            var storage = new InMemoryStorage();
            storage.Set(SessionData.StorageKey, "not-json");
            var client = PeakClient.Initialize(new PeakClientOptions
            {
                ApiUrl = "https://api.peak.xyz",
                ProjectApiKey = "k",
                Storage = storage,
            });
            Action act = () => client.Authenticate();
            act.Should().Throw<PeakError>().Which.Code.Should().Be(PeakErrorCode.SessionStorageMissing);
            storage.Get(SessionData.StorageKey).Should().BeNull();
        }
    }
}
