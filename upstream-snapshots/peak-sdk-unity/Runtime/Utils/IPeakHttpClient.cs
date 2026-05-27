using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Peak.Utils
{
    /// <summary>
    /// Interface for Peak HTTP client to enable mocking in tests
    /// </summary>
    public interface IPeakHttpClient
    {
        UniTask<T> GetAsync<T>(string endpoint, Dictionary<string, string> headers = null);
        UniTask<T> PostAsync<T>(string endpoint, object payload = null, Dictionary<string, string> headers = null);
    }
}