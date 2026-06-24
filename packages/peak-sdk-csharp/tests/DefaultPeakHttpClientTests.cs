using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KyuzanInc.Peak.Sdk.Models;
using KyuzanInc.Peak.Sdk.Utils;
using Xunit;

namespace KyuzanInc.Peak.Sdk.Tests
{
    // Covers DefaultPeakHttpClient.SendAsync response branches on the STJ-only
    // path: success deserialization, the catch(JsonException) -> InvalidResponse
    // mapping, the empty-body short-circuit, and the HTTP-error mapping.
    public class DefaultPeakHttpClientTests
    {
        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode status;
            private readonly string body;
            public StubHandler(HttpStatusCode status, string body)
            {
                this.status = status;
                this.body = body;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }

        private static DefaultPeakHttpClient ClientReturning(HttpStatusCode status, string body) =>
            new DefaultPeakHttpClient("https://api.peak.xyz", "test-key", new HttpClient(new StubHandler(status, body)));

        // Captures the outgoing HttpRequestMessage so tests can assert on the
        // headers the SDK actually puts on the wire (e.g. User-Agent).
        private sealed class CapturingHandler : HttpMessageHandler
        {
            public HttpRequestMessage? LastRequest { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"otpId\":\"otp-1\"}"),
                });
            }
        }

        [Fact]
        public async Task SendAsync_AlwaysSendsNonEmptyUserAgent()
        {
            // Peak's edge (nginx) 403s requests with an empty/absent User-Agent;
            // the SDK must always send one. Regression test for the alpha.3 fix.
            var handler = new CapturingHandler();
            var client = new DefaultPeakHttpClient("https://api.peak.xyz", "test-key", new HttpClient(handler));

            await client.GetAsync<InitOtpLoginResponse>("public-api/v1/auth/otp/init-login");

            handler.LastRequest.Should().NotBeNull();
            handler.LastRequest!.Headers.UserAgent.Should().NotBeEmpty();
            var ua = handler.LastRequest.Headers.UserAgent.ToString();
            ua.Should().NotBeNullOrWhiteSpace();
            ua.Should().StartWith("KyuzanInc.Peak.Sdk/");
        }

        [Fact]
        public async Task SendAsync_CallerSuppliedUserAgent_IsNotOverridden()
        {
            // A caller-provided User-Agent (via the headers argument) wins; the SDK
            // only fills in its default when none is present.
            var handler = new CapturingHandler();
            var client = new DefaultPeakHttpClient("https://api.peak.xyz", "test-key", new HttpClient(handler));
            var headers = new Dictionary<string, string> { ["User-Agent"] = "my-app/9.9" };

            await client.GetAsync<InitOtpLoginResponse>("public-api/v1/auth/otp/init-login", headers);

            handler.LastRequest.Should().NotBeNull();
            var uaValues = handler.LastRequest!.Headers.GetValues("User-Agent").ToList();
            uaValues.Should().ContainSingle().Which.Should().Be("my-app/9.9");
        }

        [Fact]
        public async Task GetAsync_PublicDto_DeserializesSuccessBody()
        {
            var client = ClientReturning(HttpStatusCode.OK, "{\"otpId\":\"otp-9\"}");
            var dto = await client.GetAsync<InitOtpLoginResponse>("public-api/v1/auth/otp/init-login");
            dto!.OtpId.Should().Be("otp-9");
        }

        [Fact]
        public async Task GetAsync_MalformedBody_MapsToInvalidResponse()
        {
            var client = ClientReturning(HttpStatusCode.OK, "{ not valid json");
            Func<Task> act = () => client.GetAsync<InitOtpLoginResponse>("public-api/v1/x");
            (await act.Should().ThrowAsync<PeakError>()).Which.Code.Should().Be(PeakErrorCode.InvalidResponse);
        }

        [Fact]
        public async Task GetAsync_NonStringEnumToken_MapsToInvalidResponse()
        {
            // A non-string chainType token now fails closed (STJ rejects 1 into a
            // string? field) instead of being softened to null by the old resolver.
            var client = ClientReturning(HttpStatusCode.OK, "{\"id\":\"a1\",\"chainType\":1}");
            Func<Task> act = () => client.GetAsync<AccountAddressResponse>("public-api/v1/x");
            (await act.Should().ThrowAsync<PeakError>()).Which.Code.Should().Be(PeakErrorCode.InvalidResponse);
        }

        [Fact]
        public async Task GetAsync_UnknownEnumString_PassesThroughRaw()
        {
            // An unknown chainType (e.g. a future "aptos") now passes through as the
            // raw string rather than being nulled by the removed tolerant resolver.
            var client = ClientReturning(HttpStatusCode.OK, "{\"id\":\"a1\",\"chainType\":\"aptos\"}");
            var dto = await client.GetAsync<AccountAddressResponse>("public-api/v1/x");
            dto!.ChainType.Should().Be("aptos");
        }

        [Fact]
        public async Task GetAsync_DecimalAccountIndexToken_MapsToInvalidResponse()
        {
            // accountIndex is int on the public DTO; a decimal-bearing token (1.0 or
            // 1.5) is rejected by STJ. This restores the pre-#14 STJ-into-int
            // contract (spec R8) — #14's generated path accepted 1.0 as 1.
            var oneDotZero = ClientReturning(HttpStatusCode.OK, "{\"id\":\"a\",\"accountIndex\":1.0}");
            (await ((Func<Task>)(() => oneDotZero.GetAsync<AccountResponse>("public-api/v1/x")))
                .Should().ThrowAsync<PeakError>()).Which.Code.Should().Be(PeakErrorCode.InvalidResponse);

            var oneDotFive = ClientReturning(HttpStatusCode.OK, "{\"id\":\"a\",\"accountIndex\":1.5}");
            (await ((Func<Task>)(() => oneDotFive.GetAsync<AccountResponse>("public-api/v1/x")))
                .Should().ThrowAsync<PeakError>()).Which.Code.Should().Be(PeakErrorCode.InvalidResponse);
        }

        [Fact]
        public async Task GetAsync_IntegerAccountIndexToken_Deserializes()
        {
            var client = ClientReturning(HttpStatusCode.OK, "{\"id\":\"a\",\"accountIndex\":2}");
            var dto = await client.GetAsync<AccountResponse>("public-api/v1/x");
            dto!.AccountIndex.Should().Be(2);
        }

        [Fact]
        public async Task GetAsync_MissingRequiredFields_SilentlyDefault()
        {
            // Restored pre-#14 contract (spec §6.3 / R6): the public DTOs are not
            // [JsonRequired], so a response missing fields deserializes with those
            // fields at their defaults rather than throwing. Consumers MUST
            // null/empty-check identity/address fields before use (README response-
            // validation note, Task 8). #14's generated DTOs hard-failed this.
            var client = ClientReturning(HttpStatusCode.OK, "{}");
            var dto = await client.GetAsync<AccountResponse>("public-api/v1/x");
            dto.Should().NotBeNull();
            dto!.Id.Should().BeNull();
            dto.AccountIndex.Should().Be(0);
        }

        [Fact]
        public async Task GetAsync_EmptyBody_ReturnsNull()
        {
            var client = ClientReturning(HttpStatusCode.OK, string.Empty);
            var dto = await client.GetAsync<InitOtpLoginResponse>("public-api/v1/x");
            dto.Should().BeNull();
        }

        [Fact]
        public async Task GetAsync_HttpErrorStatus_MapsToHttpError()
        {
            var client = ClientReturning(HttpStatusCode.InternalServerError, "boom");
            Func<Task> act = () => client.GetAsync<InitOtpLoginResponse>("public-api/v1/x");
            (await act.Should().ThrowAsync<PeakError>()).Which.Code.Should().Be(PeakErrorCode.HttpError);
        }
    }
}
