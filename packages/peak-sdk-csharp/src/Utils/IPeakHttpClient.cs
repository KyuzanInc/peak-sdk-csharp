using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KyuzanInc.Peak.Sdk.Utils
{
    /// <summary>
    /// Tiny HTTP client abstraction the SDK uses for peak-server traffic.
    /// Equivalent to <c>IPeakHttpClient</c> in <c>peak-sdk-unity</c>. The
    /// default implementation is <see cref="DefaultPeakHttpClient"/>; consumers
    /// can inject their own (e.g. wrapping <c>IHttpClientFactory</c>) for tests
    /// or platform-specific retry policies.
    /// </summary>
    /// <remarks>
    /// For endpoints whose response type comes from the internal
    /// <c>KyuzanInc.Peak.PublicApiClient</c> assembly, a custom implementation
    /// must deserialize the body with Newtonsoft.Json (for example
    /// <c>JsonConvert.DeserializeObject(body, typeof(T))</c>) because those types
    /// are Newtonsoft-shaped. The default <see cref="DefaultPeakHttpClient"/>
    /// does this automatically.
    /// </remarks>
    public interface IPeakHttpClient
    {
        Task<T?> GetAsync<T>(string endpoint, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default) where T : class;
        Task<T?> PostAsync<TBody, T>(string endpoint, TBody payload, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default) where TBody : class where T : class;
        Task<T?> PostAsync<T>(string endpoint, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default) where T : class;
    }
}
