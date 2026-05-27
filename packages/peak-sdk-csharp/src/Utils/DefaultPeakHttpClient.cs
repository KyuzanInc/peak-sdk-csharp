// Generic-NET replacement for the Unity-only PeakHttpClient (which used
// UnityWebRequest). Uses System.Net.Http.HttpClient. Recommended consumer
// pattern is to inject IHttpClientFactory.CreateClient(); the default
// implementation is a single shared HttpClient per process — acceptable for
// short-running console / mobile uses, less ideal for server-side flows
// where IHttpClientFactory's per-request DNS rotation matters.

using System;
using System.Collections.Generic;
using System.Net.Http;
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
    /// <see cref="System.Net.Http.HttpClient"/>. Uses
    /// <see cref="PeakJsonContext"/> for AOT / IL2CPP-safe (de)serialisation.
    /// </summary>
    public sealed class DefaultPeakHttpClient : IPeakHttpClient
    {
        private static readonly HttpClient SharedClient = new HttpClient();

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
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
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

                var typeInfo = (JsonTypeInfo<T>?)PeakJsonContext.Default.GetTypeInfo(typeof(T));
                if (typeInfo is null)
                {
                    throw new PeakError(PeakErrorCode.InvalidArgument,
                        $"Type {typeof(T).Name} is not registered in PeakJsonContext.");
                }
                return JsonSerializer.Deserialize(body!, typeInfo);
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
