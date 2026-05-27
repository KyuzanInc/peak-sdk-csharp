// Windows-only ISecureStorage backed by DPAPI (per-user data protection).
// Compiles only when the TargetFramework is net8.0-windows.
// Per docs/security/storage-threat-model.md, this is the only built-in
// secure-storage backend in v0.1.0; non-Windows hosts get
// ISecureStorage.IsAvailable == false and are expected to wire their own.

#if WINDOWS || NET8_0_WINDOWS
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KyuzanInc.Peak.Sdk.Storage
{
    /// <summary>
    /// DPAPI-backed <see cref="ISecureStorage"/>. Encrypts every value at rest
    /// under the current user's profile; blobs do not roam to other users
    /// or machines.
    ///
    /// Persistence path: <c>%LOCALAPPDATA%\KyuzanInc\PeakSdk\&lt;namespace&gt;\</c>
    /// (override with the constructor).
    /// </summary>
    public sealed class DpapiSecureStorage : ISecureStorage
    {
        private readonly string baseDirectory;

        public bool IsAvailable => true;

        public DpapiSecureStorage(string? baseDirectory = null, string @namespace = "default")
        {
            // Sanitise the namespace: only [A-Za-z0-9._-] allowed, no path
            // separators / drive letters / parent refs. This blocks path
            // traversal via a hostile @namespace argument.
            if (string.IsNullOrEmpty(@namespace)) @namespace = "default";
            foreach (var c in @namespace)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
                       || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-';
                if (!ok)
                {
                    throw new ArgumentException(
                        $"DpapiSecureStorage namespace must match [A-Za-z0-9._-]+ (got: '{@namespace}')",
                        nameof(@namespace));
                }
            }

            baseDirectory ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KyuzanInc",
                "PeakSdk",
                @namespace);
            Directory.CreateDirectory(baseDirectory);
            this.baseDirectory = baseDirectory;
        }

        private string PathFor(string key)
        {
            // Hex-encode the key so filesystem-illegal characters never reach disk.
            var bytes = Encoding.UTF8.GetBytes(key);
            return Path.Combine(baseDirectory, BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant() + ".dpapi");
        }

        public string? Get(string key)
        {
            var path = PathFor(key);
            if (!File.Exists(path)) return null;
            var encrypted = File.ReadAllBytes(path);
            var plain = ProtectedData.Unprotect(encrypted, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }

        public void Set(string key, string value)
        {
            var path = PathFor(key);
            var plain = Encoding.UTF8.GetBytes(value);
            var encrypted = ProtectedData.Protect(plain, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path, encrypted);
        }

        public void Delete(string key)
        {
            var path = PathFor(key);
            if (File.Exists(path)) File.Delete(path);
        }

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(Get(key));
        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default) { Set(key, value); return Task.CompletedTask; }
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) { Delete(key); return Task.CompletedTask; }
    }
}
#endif
