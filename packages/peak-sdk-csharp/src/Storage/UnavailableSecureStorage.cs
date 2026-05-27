using System;
using System.Threading;
using System.Threading.Tasks;

namespace KyuzanInc.Peak.Sdk.Storage
{
    /// <summary>
    /// Placeholder <see cref="ISecureStorage"/> for platforms with no built-in
    /// secure backend (Linux .NET, macOS .NET, Godot, console). All operations
    /// throw <see cref="PeakError"/> with <c>SDK_INVALID_ARGUMENT</c> after
    /// reporting <see cref="IsAvailable"/> == <c>false</c>.
    ///
    /// Consumers MUST check <c>IsAvailable</c> before persisting High or
    /// Critical assets (see <c>docs/security/storage-threat-model.md</c>).
    /// </summary>
    public sealed class UnavailableSecureStorage : ISecureStorage
    {
        public bool IsAvailable => false;

        public string? Get(string key) => throw NotAvailable();
        public void Set(string key, string value) => throw NotAvailable();
        public void Delete(string key) => throw NotAvailable();
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) => throw NotAvailable();
        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default) => throw NotAvailable();
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => throw NotAvailable();

        private static Exception NotAvailable() =>
            new PeakError(PeakErrorCode.InvalidArgument,
                "ISecureStorage is not available on this platform. Wire a platform-specific implementation " +
                "or use InMemoryStorage explicitly. See docs/security/storage-threat-model.md.");
    }
}
