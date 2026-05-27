using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace KyuzanInc.Peak.Sdk.Storage
{
    /// <summary>
    /// Process-memory, thread-safe storage. Default for every <see cref="PeakClient"/>
    /// that doesn't supply its own backend. Loses state on process exit, which
    /// is intentional: persistence is a deliberate consumer choice
    /// (see <c>docs/security/storage-threat-model.md</c>).
    /// </summary>
    public sealed class InMemoryStorage : IStorage
    {
        private readonly ConcurrentDictionary<string, string> store = new();

        public string? Get(string key) => store.TryGetValue(key, out var v) ? v : null;
        public void Set(string key, string value) => store[key] = value;
        public void Delete(string key) => store.TryRemove(key, out _);

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(Get(key));
        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            Set(key, value);
            return Task.CompletedTask;
        }
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            Delete(key);
            return Task.CompletedTask;
        }
    }
}
