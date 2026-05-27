using System;
using Cysharp.Threading.Tasks;
using Peak.Models.Api;
using Peak.Models.Sdk;
using Peak.Services.Unauthenticated;
using Peak.Utils;
using Peak.Exceptions;
using Peak.Settings;
using UnityEngine;

namespace Peak
{
    /// <summary>
    /// Main entry point for the Peak SDK providing access to unauthenticated operations.
    /// </summary>
    /// <remarks>
    /// The PeakSdk class handles user authentication and basic user operations
    /// that don't require an authenticated session. For operations requiring authentication,
    /// use the <see cref="Authenticate()"/> method to create an <see cref="AuthenticatedPeakSdk"/> instance.
    ///
    /// User creation is handled by the OTP login flow. By default, CompleteOtpLoginAsync
    /// creates a user if the email is not registered (signup=true). Use signup=false for login-only flows.
    ///
    /// This SDK follows an authentication pattern similar to AWS SDK, where you first
    /// initialize an unauthenticated client, then create authenticated instances as needed.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Initialize the SDK using Unity Editor settings
    /// var sdk = PeakSdk.Initialize();
    ///
    /// // Complete OTP login flow
    /// var otpResult = await sdk.InitOtpLoginAsync("user@example.com");
    /// var loginResult = await sdk.CompleteOtpLoginAsync(
    ///     "user@example.com",
    ///     otpResult.OtpId,
    ///     "123456"
    /// );
    ///
    /// // Create authenticated instance after login
    /// var authSdk = sdk.Authenticate();
    /// </code>
    /// </example>
    public class PeakSdk
    {
        private readonly SdkOptions options;
        private readonly AuthService authService;

        private PeakSdk(SdkOptions options)
        {
            this.options = options;
            this.authService = new AuthService(options.ApiUrl, options.ProjectApiKey);
        }

        /// <summary>
        /// Initializes the Peak SDK using configuration from Unity Editor settings.
        /// </summary>
        /// <returns>A configured PeakSdk instance ready for use.</returns>
        /// <exception cref="SdkException">
        /// Thrown when initialization fails due to missing or invalid settings.
        /// </exception>
        /// <remarks>
        /// This method automatically loads configuration from PeakSDKSettings asset
        /// created via the 'Peak SDK > Settings' menu in Unity Editor. It performs
        /// validation of all required settings and ensures security configurations
        /// such as .gitignore entries are properly set up.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Initialize SDK with Unity Editor settings
        /// var sdk = PeakSdk.Initialize();
        ///
        /// // The SDK is now ready for use
        /// var otpResult = await sdk.InitOtpLoginAsync("user@example.com");
        /// </code>
        /// </example>
        /// <seealso cref="Initialize(SdkOptions)"/>
        public static PeakSdk Initialize()
        {
            try
            {
                // Load settings from Unity Editor configuration
                var settings = PeakSDKSettings.LoadFromResources();

                if (settings == null)
                {
                    throw new SdkException(SdkErrorCodes.INITIALIZATION_FAILED,
                        "Peak SDK settings not found. Please create settings via 'Peak SDK > Settings' menu.");
                }

                // Validate settings before using them
                var validationResult = settings.ValidateSettings();
                if (!validationResult.IsValid)
                {
                    var errors = string.Join(", ", validationResult.Errors);
                    throw new SdkException(SdkErrorCodes.INITIALIZATION_FAILED,
                        $"Peak SDK settings validation failed: {errors}");
                }

                // Log validation warnings if any
                if (validationResult.Warnings.Length > 0)
                {
                    var warnings = string.Join(", ", validationResult.Warnings);
                    Debug.LogWarning($"[Peak SDK] Configuration warnings: {warnings}");
                }

                // Get SdkOptions from settings
                var options = settings.GetSdkOptions();

                // Log configuration info (masked for security)
                if (settings.EnableDebugLogging)
                {
                    Debug.Log($"[Peak SDK] Initializing with environment: {settings.Environment}");
                    Debug.Log($"[Peak SDK] API URL: {settings.CurrentApiUrl}");
                    Debug.Log($"[Peak SDK] Project API Key: {settings.GetMaskedApiKey()}");
                }

                return Initialize(options);
            }
            catch (SdkException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SdkException(SdkErrorCodes.INITIALIZATION_FAILED,
                    "Failed to initialize Peak SDK from settings", ex);
            }
        }

        /// <summary>
        /// Initializes the Peak SDK with custom configuration options.
        /// </summary>
        /// <param name="options">The SDK configuration containing API URL and project API key.</param>
        /// <returns>A configured PeakSdk instance ready for use.</returns>
        /// <exception cref="SdkException">
        /// Thrown when initialization fails due to invalid configuration.
        /// </exception>
        /// <remarks>
        /// Use this overload when you need to programmatically configure the SDK
        /// instead of using Unity Editor settings. The configuration is validated
        /// before initialization completes.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Initialize SDK with custom options
        /// var options = new SdkOptions
        /// {
        ///     ProjectApiKey = "your-project-api-key",
        ///     ApiUrl = "https://api.peak.xyz"
        /// };
        ///
        /// var sdk = PeakSdk.Initialize(options);
        /// </code>
        /// </example>
        /// <seealso cref="Initialize()"/>
        /// <seealso cref="SdkOptions"/>
        public static PeakSdk Initialize(SdkOptions options)
        {
            try
            {
                // Validate configuration
                ConfigValidator.ValidateInitializationOptions(options);

                // Log platform information
                Debug.Log($"[Peak SDK] Initialized on platform: {PlatformHelper.GetPlatformName()}");
                Debug.Log($"[Peak SDK] Mobile platform: {PlatformHelper.IsMobilePlatform()}");

                return new PeakSdk(options);
            }
            catch (SdkException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SdkException(SdkErrorCodes.INITIALIZATION_FAILED,
                    "Failed to initialize Peak SDK", ex);
            }
        }

        /// <summary>
        /// Initiates the OTP login process by sending a one-time password to the user's email.
        /// </summary>
        /// <param name="email">The email address of the user attempting to log in.</param>
        /// <returns>
        /// A task that resolves to an InitOtpLoginResponse containing the OTP ID
        /// required for completing the login process.
        /// </returns>
        /// <exception cref="SdkException">
        /// Thrown when the network request fails or the email address is invalid.
        /// </exception>
        /// <remarks>
        /// This is the first step in the two-step OTP authentication flow.
        /// The OTP ID returned by this method must be provided to <see cref="CompleteOtpLoginAsync(string, string, string)"/>
        /// along with the OTP code sent to the user's email.
        ///
        /// The OTP code is valid for a limited time (typically 5 minutes).
        /// </remarks>
        /// <example>
        /// <code>
        /// var sdk = PeakSdk.Initialize();
        ///
        /// // Step 1: Initiate OTP login
        /// var otpResult = await sdk.InitOtpLoginAsync("user@example.com");
        /// Debug.Log($"OTP sent. ID: {otpResult.OtpId}");
        ///
        /// // User receives OTP via email
        /// // Step 2: Complete login with OTP code
        /// </code>
        /// </example>
        /// <seealso cref="CompleteOtpLoginAsync(string, string, string)"/>
        public async UniTask<InitOtpLoginResponse> InitOtpLoginAsync(string email)
        {
            try
            {
                return await authService.InitOtpLoginAsync(email);
            }
            catch (HttpException ex)
            {
                throw new SdkException(SdkErrorCodes.NETWORK_ERROR,
                    $"Init OTP login failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Completes the OTP login process by verifying the code and obtaining session credentials.
        /// </summary>
        /// <param name="email">The email address of the user logging in.</param>
        /// <param name="otpId">The OTP ID obtained from InitOtpLoginAsync.</param>
        /// <param name="otpCode">The 6-digit OTP code sent to the user's email.</param>
        /// <param name="signup">
        /// If true (default), creates user when missing. If false, returns error when user not found.
        /// </param>
        /// <returns>
        /// A task that resolves to a CompleteOtpLoginResult containing the session JWT
        /// and key pair for authenticated operations.
        /// </returns>
        /// <exception cref="SdkException">
        /// Thrown when authentication fails due to invalid OTP code, expired OTP,
        /// or network errors. Also thrown when signup=false and user does not exist.
        /// </exception>
        /// <remarks>
        /// This is the second step in the two-step OTP authentication flow.
        /// Upon successful completion, this method:
        /// - Generates a P256 key pair for Turnkey operations
        /// - Obtains a session JWT for API authentication
        /// - Creates user if missing and signup=true (default)
        /// - Saves the session data via <see cref="SessionStorage"/>, which uses Unity PlayerPrefs
        ///
        /// Session credentials persisted through PlayerPrefs can be restored
        /// using <see cref="Authenticate()"/>. For production use, consider replacing
        /// the default storage with a platform-secure alternative.
        /// </remarks>
        /// <example>
        /// <code>
        /// var sdk = PeakSdk.Initialize();
        ///
        /// // Step 1: Initiate OTP login
        /// var otpResult = await sdk.InitOtpLoginAsync("user@example.com");
        ///
        /// // Step 2: Complete login with OTP code from email (creates user if missing)
        /// var loginResult = await sdk.CompleteOtpLoginAsync(
        ///     "user@example.com",
        ///     otpResult.OtpId,
        ///     "123456"
        /// );
        ///
        /// // For login-only (no user creation):
        /// // var loginResult = await sdk.CompleteOtpLoginAsync(email, otpId, otpCode, signup: false);
        ///
        /// // Session is now saved, create authenticated SDK
        /// var authSdk = sdk.Authenticate();
        /// </code>
        /// </example>
        /// <seealso cref="InitOtpLoginAsync(string)"/>
        /// <seealso cref="Authenticate()"/>
        public async UniTask<CompleteOtpLoginResult> CompleteOtpLoginAsync(string email, string otpId, string otpCode, bool signup = true)
        {
            try
            {
                var result = await authService.CompleteOtpLoginAsync(email, otpId, otpCode, signup);

                SessionStorage.Clear();
                SessionStorage.Save(new SessionStorage.SessionData
                {
                    email = email,
                    sessionJwt = result.sessionJwt,
                    targetPrivateKey = result.keyPair?.privateKey,
                    targetPublicKey = result.keyPair?.publicKey,
                    savedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });

                return result;
            }
            catch (HttpException ex)
            {
                throw new SdkException(SdkErrorCodes.AUTHENTICATION_FAILED,
                    $"Complete OTP login failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Logs out the current user by clearing the stored authentication session.
        /// Equivalent to logout() in Browser SDK.
        /// </summary>
        /// <remarks>
        /// This method should be called when:
        /// - The user explicitly logs out
        /// - Switching between user accounts
        /// - Session cleanup is required for security reasons
        ///
        /// After calling this method, <see cref="Authenticate()"/> will throw
        /// <see cref="NotAuthenticatedException"/> until the user completes a new login flow.
        /// The default implementation clears values stored in Unity PlayerPrefs via <see cref="SessionStorage"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// var sdk = PeakSdk.Initialize();
        ///
        /// // Log out the user
        /// sdk.Logout();
        /// Debug.Log("User logged out, session cleared");
        /// </code>
        /// </example>
        /// <seealso cref="Authenticate()"/>
        public void Logout()
        {
            SessionStorage.Clear();
        }

        /// <summary>
        /// Creates an authenticated SDK instance using stored session credentials.
        /// Validates JWT signature, expiry, and public key to align with Browser SDK.
        /// </summary>
        /// <returns>
        /// An AuthenticatedPeakSdk instance configured with the current session.
        /// </returns>
        /// <exception cref="NotAuthenticatedException">
        /// Thrown when no valid authentication data is found in storage.
        /// </exception>
        /// <exception cref="TokenExpiredException">
        /// Thrown when the session JWT has expired. The stored session is automatically cleared.
        /// </exception>
        /// <exception cref="SdkException">
        /// Thrown for other authentication failures (invalid signature, public key mismatch, etc.).
        /// The stored session is automatically cleared if invalid.
        /// </exception>
        /// <remarks>
        /// This method requires that the user has previously completed the OTP login flow
        /// via <see cref="CompleteOtpLoginAsync(string, string, string)"/>. Session credentials are
        /// loaded through <see cref="SessionStorage"/>, which defaults to Unity PlayerPrefs.
        /// Aligned with authenticate() in peak-sdk-browser.
        /// </remarks>
        /// <example>
        /// <code>
        /// var sdk = PeakSdk.Initialize();
        ///
        /// // After successful login
        /// try
        /// {
        ///     var authSdk = sdk.Authenticate();
        ///     // Use for authenticated operations
        ///     var accounts = await authSdk.ListAccountsAsync();
        /// }
        /// catch (TokenExpiredException)
        /// {
        ///     // Session expired, redirect to login
        /// }
        /// catch (NotAuthenticatedException)
        /// {
        ///     // No session found, redirect to login
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="AuthenticatedPeakSdk"/>
        /// <seealso cref="CompleteOtpLoginAsync(string, string, string)"/>
        public AuthenticatedPeakSdk Authenticate()
        {
            var stored = SessionStorage.Load();
            if (stored == null)
            {
                throw new NotAuthenticatedException(
                    "Authentication data not found. Please complete OTP login first.");
            }

            try
            {
                var authOptions = BuildAuthenticationOptionsFrom(stored);
                return CreateAuthenticatedSdk(authOptions);
            }
            catch (NotAuthenticatedException)
            {
                SessionStorage.Clear();
                throw;
            }
            catch (TokenExpiredException)
            {
                SessionStorage.Clear();
                throw;
            }
            catch (SdkException)
            {
                SessionStorage.Clear();
                throw;
            }
        }

        private AuthenticatedPeakSdk CreateAuthenticatedSdk(AuthenticationOptions authOptions)
        {
            try
            {
                if (authOptions == null)
                    throw new ArgumentNullException(nameof(authOptions));

                return new AuthenticatedPeakSdk(options, authOptions);
            }
            catch (Exception ex)
            {
                throw new SdkException(SdkErrorCodes.AUTHENTICATION_FAILED,
                    $"Authentication failed: {ex.Message}", ex);
            }
        }

        private static AuthenticationOptions BuildAuthenticationOptionsFrom(SessionStorage.SessionData stored)
        {
            // 1. Validate basic fields are present
            if (stored == null ||
                string.IsNullOrWhiteSpace(stored.email) ||
                string.IsNullOrWhiteSpace(stored.sessionJwt) ||
                string.IsNullOrWhiteSpace(stored.targetPrivateKey))
            {
                throw new NotAuthenticatedException(
                    "Stored session is invalid or incomplete. Please login again.");
            }

            // 2. Validate JWT (signature, expiry, public key match) - aligned with Browser SDK
            try
            {
                SessionJwt.ValidateSessionJwt(stored.sessionJwt, stored.targetPublicKey);
            }
            catch (TokenExpiredException)
            {
                throw;
            }
            catch (NotAuthenticatedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SdkException(SdkErrorCodes.AUTHENTICATION_FAILED,
                    $"JWT validation failed: {ex.Message}", ex);
            }

            return new AuthenticationOptions
            {
                Email = stored.email,
                SessionJwt = stored.sessionJwt,
                TargetPrivateKey = stored.targetPrivateKey
            };
        }

        private KeyPair GenerateP256KeyPair()
        {
            // TODO: Implement proper P256 key generation for mobile platforms
            // This is a placeholder - real implementation would use platform-specific crypto
            throw new NotImplementedException("P256 key generation not yet implemented");
        }
    }

    /// <summary>
    /// Configuration options for initializing the Peak SDK.
    /// </summary>
    /// <remarks>
    /// These options define the core configuration required for the SDK to
    /// connect to Peak services. Typically these values are managed through
    /// Unity Editor settings, but can be provided programmatically when needed.
    /// </remarks>
    public class SdkOptions
    {
        /// <summary>
        /// Gets or sets the project-specific API key for authenticating with Peak services.
        /// </summary>
        /// <value>
        /// The API key provided by Peak for your project.
        /// </value>
        /// <remarks>
        /// This key identifies your application to Peak services.
        /// Keep this key secure and never commit it to version control.
        /// </remarks>
        public string ProjectApiKey { get; set; }

        /// <summary>
        /// Gets or sets the base URL for Peak API endpoints.
        /// </summary>
        /// <value>
        /// The API URL, typically "https://api.peak.xyz" for production.
        /// </value>
        /// <remarks>
        /// Different environments may use different URLs:
        /// - Production: https://api.peak.xyz
        /// - Development: Custom development server URL
        /// </remarks>
        public string ApiUrl { get; set; }
    }

    /// <summary>
    /// Authentication credentials required for creating an authenticated SDK instance.
    /// </summary>
    /// <remarks>
    /// These options contain the session credentials obtained after successful
    /// OTP login. They are typically managed automatically by the SDK and stored
    /// securely on the device.
    /// </remarks>
    public class AuthenticationOptions
    {
        /// <summary>
        /// Gets or sets the authenticated user's email address.
        /// </summary>
        /// <value>
        /// The email address used during authentication.
        /// </value>
        public string Email { get; set; }

        /// <summary>
        /// Gets or sets the session JWT token for API authentication.
        /// </summary>
        /// <value>
        /// The JWT token obtained from successful OTP login.
        /// </value>
        /// <remarks>
        /// This token is used to authenticate API requests and has a limited lifetime.
        /// The SDK does not refresh tokens automatically; initiate a new OTP login when it expires.
        /// </remarks>
        public string SessionJwt { get; set; }

        /// <summary>
        /// Gets or sets the Turnkey target private key for cryptographic operations.
        /// </summary>
        /// <value>
        /// The P256 private key used for signing Turnkey operations.
        /// </value>
        /// <remarks>
        /// This key is generated during the login process and is used for
        /// secure wallet operations through Turnkey's infrastructure.
        /// It should be stored securely and never exposed.
        /// </remarks>
        public string TargetPrivateKey { get; set; }
    }
}
