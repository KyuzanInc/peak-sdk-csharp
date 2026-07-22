// Ported from upstream-snapshots/peak-sdk-unity/Runtime/PeakSdk.cs.
// Class renamed from PeakSdk -> PeakClient to match the TS family naming
// (peak-sdk-browser uses PeakSdk; we picked PeakClient to align with the .NET
// idiom of "Client" suffix for HTTP-talking primaries — see plan A-6).
//
// The static factory PeakClient.Initialize(...) preserves the Unity entry
// point shape; the constructor + options pattern is the new .NET-idiomatic
// path (plan D2).
//
// Authenticate() is sync; AuthenticateAsync() is async — both supplied to
// avoid the Unity-shim deadlock concern (plan D2).

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KyuzanInc.Peak.Sdk.Models;
using KyuzanInc.Peak.Sdk.Services;
using KyuzanInc.Peak.Sdk.Storage;
using KyuzanInc.Peak.Sdk.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KyuzanInc.Peak.Sdk
{
    /// <summary>
    /// Configuration for <see cref="PeakClient"/>.
    /// </summary>
    public sealed class PeakClientOptions
    {
        public required string ProjectApiKey { get; init; }

        /// <summary>
        /// Absolute HTTPS base URI for Peak API requests. User info, query,
        /// fragment, and ambiguous path-escape components are not accepted.
        /// </summary>
        public required string ApiUrl { get; init; }
        public IStorage? Storage { get; init; }
        public IPeakHttpClient? HttpClient { get; init; }
        public ILoggerFactory? LoggerFactory { get; init; }
    }

    /// <summary>
    /// Main entry point for unauthenticated Peak operations: initialise,
    /// OTP login flow, logout, and the transition into
    /// <see cref="AuthenticatedPeakClient"/>.
    /// </summary>
    public sealed class PeakClient
    {
        private readonly PeakClientOptions options;
        private readonly IStorage storage;
        private readonly IPeakHttpClient httpClient;
        private readonly ILoggerFactory loggerFactory;
        private readonly AuthService authService;

        private PeakClient(PeakClientOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.ProjectApiKey))
                throw new PeakError(PeakErrorCode.InitializationFailed, "ProjectApiKey is required");
            if (string.IsNullOrWhiteSpace(options.ApiUrl))
                throw new PeakError(PeakErrorCode.InitializationFailed, "ApiUrl is required");
            try
            {
                DefaultPeakHttpClient.ValidateBaseUri(options.ApiUrl);
            }
            catch (ArgumentException ex)
            {
                throw new PeakError(
                    PeakErrorCode.InitializationFailed,
                    "ApiUrl must be an absolute HTTPS URI without user info, query, fragment, or ambiguous path escapes.",
                    ex);
            }

            this.options = options;
            this.storage = options.Storage ?? new InMemoryStorage();
            this.loggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;
            this.httpClient = options.HttpClient ?? new DefaultPeakHttpClient(
                options.ApiUrl,
                options.ProjectApiKey,
                logger: this.loggerFactory.CreateLogger<DefaultPeakHttpClient>());
            this.authService = new AuthService(
                options.ApiUrl,
                options.ProjectApiKey,
                this.httpClient,
                logger: this.loggerFactory.CreateLogger<AuthService>());
        }

        /// <summary>Static factory matching the Unity port entry point.</summary>
        public static PeakClient Initialize(PeakClientOptions options) => new PeakClient(options);

        /// <summary>Convenience overload: pass apiUrl + projectApiKey directly.</summary>
        public static PeakClient Initialize(string apiUrl, string projectApiKey) =>
            new PeakClient(new PeakClientOptions { ApiUrl = apiUrl, ProjectApiKey = projectApiKey });

        /// <summary>Initiate the OTP login flow. Caller receives an OTP via email.</summary>
        public Task<InitOtpLoginResponse?> InitOtpLoginAsync(string email, CancellationToken cancellationToken = default) =>
            authService.InitOtpLoginAsync(email, cancellationToken);

        /// <summary>
        /// Complete the OTP login flow, generate the P-256 key pair, save the
        /// session to <see cref="IStorage"/> at <see cref="SessionData.StorageKey"/>.
        /// </summary>
        public async Task<CompleteOtpLoginResult> CompleteOtpLoginAsync(
            string email, string otpId, string otpCode, bool signup = true, CancellationToken cancellationToken = default)
        {
            var result = await authService.CompleteOtpLoginAsync(email, otpId, otpCode, signup, cancellationToken).ConfigureAwait(false);

            // Persist session into IStorage. Unity port behaviour: clear any
            // prior session before writing so a partial write failure cannot
            // leave a stale session reachable. JSON is the wire format; the
            // SDK never reads SessionData.JSON outside this layer.
            storage.Delete(SessionData.StorageKey);
            var sessionData = new SessionData
            {
                Email = email,
                SessionJwt = result.SessionJwt,
                TargetPrivateKey = result.KeyPair?.PrivateKey,
                TargetPublicKey = result.KeyPair?.PublicKey,
                SavedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            storage.Set(SessionData.StorageKey,
                JsonSerializer.Serialize(sessionData, PeakJsonContext.Default.SessionData));

            return result;
        }

        /// <summary>Clear the persisted session.</summary>
        public void Logout() => storage.Delete(SessionData.StorageKey);

        /// <summary>
        /// Build an <see cref="AuthenticatedPeakClient"/> from the persisted
        /// session. Synchronous variant — used by Unity-style consumers that
        /// don't want to await on resume.
        /// </summary>
        public AuthenticatedPeakClient Authenticate()
        {
            var json = storage.Get(SessionData.StorageKey);
            return AuthenticateCore(json);
        }

        /// <summary>Asynchronous variant of <see cref="Authenticate"/>.</summary>
        public async Task<AuthenticatedPeakClient> AuthenticateAsync(CancellationToken cancellationToken = default)
        {
            var json = await storage.GetAsync(SessionData.StorageKey, cancellationToken).ConfigureAwait(false);
            return AuthenticateCore(json);
        }

        private AuthenticatedPeakClient AuthenticateCore(string? sessionJson)
        {
            if (string.IsNullOrEmpty(sessionJson))
            {
                throw new PeakError(PeakErrorCode.NotAuthenticated,
                    "Authentication data not found. Please complete OTP login first.");
            }

            SessionData? stored;
            try
            {
                stored = JsonSerializer.Deserialize(sessionJson!, PeakJsonContext.Default.SessionData);
            }
            catch (Exception ex)
            {
                storage.Delete(SessionData.StorageKey);
                throw new PeakError(PeakErrorCode.SessionStorageMissing,
                    "Stored session is not a valid JSON payload. Please login again.", ex);
            }

            if (stored == null
                || string.IsNullOrWhiteSpace(stored.Email)
                || string.IsNullOrWhiteSpace(stored.SessionJwt)
                || string.IsNullOrWhiteSpace(stored.TargetPrivateKey))
            {
                storage.Delete(SessionData.StorageKey);
                throw new PeakError(PeakErrorCode.NotAuthenticated,
                    "Stored session is invalid or incomplete. Please login again.");
            }

            try
            {
                SessionJwt.ValidateSessionJwt(stored.SessionJwt!, stored.TargetPublicKey);
            }
            catch (PeakError)
            {
                storage.Delete(SessionData.StorageKey);
                throw;
            }
            catch (Exception ex)
            {
                storage.Delete(SessionData.StorageKey);
                throw new PeakError(PeakErrorCode.AuthenticationFailed, $"JWT validation failed: {ex.Message}", ex);
            }

            return new AuthenticatedPeakClient(options, stored, httpClient, loggerFactory);
        }

        // Make storage accessible to tests without exposing it
        // to consumers via the public surface.
        internal IStorage StorageForTests => storage;
    }
}
