using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KyuzanInc.Peak.Sdk.Utils;
using Xunit;
using Gen = KyuzanInc.Peak.PublicApiClient.Model;

namespace KyuzanInc.Peak.Sdk.Tests
{
    // Covers the response branches of DefaultPeakHttpClient.SendAsync that this PR
    // touched: the generated-DTO Newtonsoft path, the new
    // catch(Newtonsoft.Json.JsonException) -> InvalidResponse mapping, the
    // empty-body short-circuit, and the HTTP-error mapping.
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

        [Fact]
        public async Task GetAsync_GeneratedDto_DeserializesSuccessBody()
        {
            var client = ClientReturning(HttpStatusCode.OK, "{\"otpId\":\"otp-9\"}");
            var dto = await client.GetAsync<Gen.InitOtpLoginResponseDto>("public-api/v1/auth/otp/init-login");
            dto!.OtpId.Should().Be("otp-9");
        }

        [Fact]
        public async Task GetAsync_GeneratedDto_MalformedBody_MapsToInvalidResponse()
        {
            // Malformed JSON makes Newtonsoft throw Newtonsoft.Json.JsonException;
            // the new catch must map it to PeakErrorCode.InvalidResponse rather
            // than letting a raw JsonReaderException escape.
            var client = ClientReturning(HttpStatusCode.OK, "{ not valid json");
            Func<Task> act = () => client.GetAsync<Gen.InitOtpLoginResponseDto>("public-api/v1/x");
            (await act.Should().ThrowAsync<PeakError>()).Which.Code.Should().Be(PeakErrorCode.InvalidResponse);
        }

        [Fact]
        public async Task GetAsync_EmptyBody_ReturnsNull()
        {
            var client = ClientReturning(HttpStatusCode.OK, string.Empty);
            var dto = await client.GetAsync<Gen.InitOtpLoginResponseDto>("public-api/v1/x");
            dto.Should().BeNull();
        }

        [Fact]
        public async Task GetAsync_HttpErrorStatus_MapsToHttpError()
        {
            var client = ClientReturning(HttpStatusCode.InternalServerError, "boom");
            Func<Task> act = () => client.GetAsync<Gen.InitOtpLoginResponseDto>("public-api/v1/x");
            (await act.Should().ThrowAsync<PeakError>()).Which.Code.Should().Be(PeakErrorCode.HttpError);
        }
    }
}
