// Generic-NET replacement for the Unity-only PeakHttpClient (which used
// UnityWebRequest). Uses System.Net.Http.HttpClient. Recommended consumer
// pattern is to inject IHttpClientFactory.CreateClient(); the default
// implementation is a single shared HttpClient per process — acceptable for
// short-running console / mobile uses, less ideal for server-side flows
// where IHttpClientFactory's per-request DNS rotation matters.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KyuzanInc.Peak.Sdk.Utils
{
    /// <summary>
    /// Default <see cref="IPeakHttpClient"/> implementation backed by
    /// <see cref="System.Net.Http.HttpClient"/>. Request bodies are serialised
    /// with <see cref="PeakJsonContext"/> (AOT / IL2CPP-safe source generation);
    /// response bodies are deserialised via <c>PeakResponseJson</c> using the same
    /// source-generated context (no reflection fallback; throws if a type is not
    /// registered).
    /// </summary>
    public sealed class DefaultPeakHttpClient : IPeakHttpClient
    {
        private static readonly HttpClient SharedClient = new HttpClient();

        /// <summary>
        /// User-Agent sent on every outgoing request (e.g.
        /// <c>KyuzanInc.Peak.Sdk/0.1.0-alpha.3</c>). Peak's edge (nginx) returns
        /// 403 Forbidden for requests with an empty/absent User-Agent, so the SDK
        /// must always send one. Derived from the assembly's informational version
        /// (the full semver, including any pre-release suffix), falling back to the
        /// numeric assembly version. Set per-request in <see cref="ApplyHeaders"/>
        /// and only when the request has no User-Agent yet, so it works regardless
        /// of an injected <see cref="HttpClient"/> and never mutates a caller-owned
        /// client's shared <c>DefaultRequestHeaders</c>.
        /// </summary>
        private static readonly ProductInfoHeaderValue UserAgent = BuildUserAgent();

        private static ProductInfoHeaderValue BuildUserAgent()
        {
            var asm = typeof(DefaultPeakHttpClient).Assembly;
            // AssemblyInformationalVersion carries the full NuGet <Version>
            // (e.g. "0.1.0-alpha.3"); AssemblyName.Version drops the pre-release
            // suffix (e.g. "0.1.0.0"). Prefer the informational version, but strip
            // any "+<sha>" SourceLink build metadata which is not a valid
            // product-version token.
            var informational = asm
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            string version;
            if (!string.IsNullOrWhiteSpace(informational))
            {
                var plus = informational!.IndexOf('+');
                version = plus >= 0 ? informational.Substring(0, plus) : informational;
            }
            else
            {
                version = asm.GetName().Version?.ToString() ?? "0.0.0";
            }

            return new ProductInfoHeaderValue("KyuzanInc.Peak.Sdk", version);
        }

        private readonly string baseUrl;
        private readonly string projectApiKey;
        private readonly ILogger<DefaultPeakHttpClient> logger;
        private readonly HttpClient httpClient;

        public DefaultPeakHttpClient(
            string baseUrl,
            string projectApiKey,
            HttpClient? httpClient = null,
            ILogger<DefaultPeakHttpClient>? logger = null)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentException(nameof(baseUrl));
            this.baseUrl = baseUrl.TrimEnd('/');
            this.projectApiKey = projectApiKey ?? string.Empty;
            this.httpClient = httpClient ?? SharedClient;
            this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultPeakHttpClient>.Instance;
        }

        public async Task<T?> GetAsync<T>(string endpoint, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default) where T : class
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, BuildUri(endpoint));
            ApplyHeaders(req, headers);
            return await SendAsync<T>(req, cancellationToken).ConfigureAwait(false);
        }

        public async Task<T?> PostAsync<TBody, T>(string endpoint, TBody payload, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default) where TBody : class where T : class
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, BuildUri(endpoint));
            ApplyHeaders(req, headers);

            var typeInfo = (JsonTypeInfo<TBody>?)PeakJsonContext.Default.GetTypeInfo(typeof(TBody));
            if (typeInfo is null)
            {
                throw new PeakError(PeakErrorCode.InvalidArgument,
                    $"Type {typeof(TBody).Name} is not registered in PeakJsonContext. " +
                    "Add [JsonSerializable(typeof(...))] in PeakJsonContext.cs.");
            }

            var json = JsonSerializer.Serialize(payload, typeInfo);
            logger.LogDebug("POST {Endpoint} body={Body}", endpoint, json);
            // Use a ByteArrayContent + explicit Content-Type header so the
            // wire value is exactly `application/json` (Unity-compatible)
            // rather than the .NET default of `application/json; charset=utf-8`
            // that StringContent appends.
            var bodyBytes = Encoding.UTF8.GetBytes(json);
            var content = new ByteArrayContent(bodyBytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            req.Content = content;
            return await SendAsync<T>(req, cancellationToken).ConfigureAwait(false);
        }

        public async Task<T?> PostAsync<T>(string endpoint, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default) where T : class
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, BuildUri(endpoint));
            ApplyHeaders(req, headers);
            return await SendAsync<T>(req, cancellationToken).ConfigureAwait(false);
        }

        private Uri BuildUri(string endpoint)
        {
            var trimmed = endpoint.StartsWith("/", StringComparison.Ordinal) ? endpoint.Substring(1) : endpoint;
            return new Uri($"{baseUrl}/{trimmed}");
        }

        private void ApplyHeaders(HttpRequestMessage req, IReadOnlyDictionary<string, string>? headers)
        {
            if (!string.IsNullOrEmpty(projectApiKey))
            {
                req.Headers.TryAddWithoutValidation("X-API-KEY", projectApiKey);
            }
            if (headers != null)
            {
                foreach (var kv in headers)
                {
                    req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                }
            }

            // Peak's edge (nginx) returns 403 Forbidden when User-Agent is empty
            // or absent. Set it per-request (never on the shared/injected client's
            // DefaultRequestHeaders) and only when the caller has not already
            // supplied one — either via the `headers` argument above (which goes
            // through TryAddWithoutValidation, so check Contains rather than the
            // strongly-typed UserAgent collection) or on an injected client.
            if (!req.Headers.Contains("User-Agent"))
            {
                req.Headers.UserAgent.Add(UserAgent);
            }
        }

        private async Task<T?> SendAsync<T>(HttpRequestMessage req, CancellationToken cancellationToken) where T : class
        {
            HttpResponseMessage? response = null;
            string? body = null;
            try
            {
                response = await httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new PeakError(
                        PeakErrorCode.HttpError,
                        $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                        apiResponse: new ApiResponseContext
                        {
                            HttpStatusCode = (int)response.StatusCode,
                            Endpoint = req.RequestUri?.AbsolutePath,
                            Method = req.Method.Method,
                            RawResponseBody = body,
                        });
                }

                if (string.IsNullOrEmpty(body)) return null;

                return PeakResponseJson.Deserialize<T>(body!);
            }
            catch (PeakError) { throw; }
            catch (HttpRequestException ex)
            {
                throw new PeakError(PeakErrorCode.NetworkError, $"Network request failed: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new PeakError(PeakErrorCode.NetworkError, $"Request timed out: {ex.Message}", ex);
            }
            // A parse failure surfaces as System.Text.Json.JsonException (the only
            // serialiser on the response path). Map it to InvalidResponse with the
            // raw body.
            catch (JsonException ex)
            {
                throw new PeakError(PeakErrorCode.InvalidResponse,
                    $"Failed to parse JSON response: {ex.Message}", ex,
                    new ApiResponseContext
                    {
                        HttpStatusCode = response != null ? (int)response.StatusCode : (int?)null,
                        Endpoint = req.RequestUri?.AbsolutePath,
                        Method = req.Method.Method,
                        RawResponseBody = body,
                    });
            }
            finally
            {
                response?.Dispose();
            }
        }
    }
}
