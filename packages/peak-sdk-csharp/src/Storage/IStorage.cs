using System.Threading;
using System.Threading.Tasks;

namespace KyuzanInc.Peak.Sdk.Storage
{
    /// <summary>
    /// Key/value storage abstraction used by <see cref="PeakClient"/>.
    /// Default implementation is <see cref="InMemoryStorage"/>; consumers wire
    /// in their own implementation (DPAPI, Unity Keychain, etc.) for
    /// persistence.
    ///
    /// Both sync and async overloads are provided. The default implementation
    /// of the async variants delegates to the sync overloads via
    /// <see cref="Task.FromResult{TResult}(TResult)"/>.
    /// </summary>
    public interface IStorage
    {
        string? Get(string key);
        void Set(string key, string value);
        void Delete(string key);

        Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
        Task SetAsync(string key, string value, CancellationToken cancellationToken = default);
        Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Marker extension of <see cref="IStorage"/> that promises an
    /// OS-protected backend (DPAPI, Keychain, KeyStore). Consumers check
    /// <see cref="IsAvailable"/> before persisting High or Critical assets;
    /// see <c>docs/security/storage-threat-model.md</c>.
    /// </summary>
    public interface ISecureStorage : IStorage
    {
        bool IsAvailable { get; }
    }
}
