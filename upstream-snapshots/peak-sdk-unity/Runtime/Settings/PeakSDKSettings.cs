using System;
using UnityEngine;

namespace Peak.Settings
{
    /// <summary>
    /// Main configuration settings for Peak SDK
    /// This ScriptableObject stores environment-specific settings and sensitive credentials
    /// </summary>
    [CreateAssetMenu(fileName = "PeakSettings", menuName = "Peak SDK/Settings", order = 1)]
    public class PeakSDKSettings : ScriptableObject
    {
        private const string ResourcesPath = "PeakSettings";
        private static PeakSDKSettings _instance;

        [Header("Environment Configuration")]
        [SerializeField] private EnvironmentType environment = EnvironmentType.Development;

        [Header("API Endpoints")]
        [SerializeField] private string developmentApiUrl = "https://api.peak-dev.xyz";
        [SerializeField] private string stagingApiUrl = "https://api.peak-staging.xyz";
        [SerializeField] private string productionApiUrl = "https://api.peak.xyz";

        [Header("Security Settings")]
        [Tooltip("Project API Key for authentication. This will be excluded from version control.")]
        [PasswordFieldAttribute("Enter Project API Key...")]
        [SerializeField] private string projectApiKey = "";

        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogging = true;
        [SerializeField] private bool validateApiConnectivity = false;

        // Properties
        public EnvironmentType Environment
        {
            get => environment;
            set => environment = value;
        }

        public string DevelopmentApiUrl
        {
            get => developmentApiUrl;
            set => developmentApiUrl = value;
        }

        public string StagingApiUrl
        {
            get => stagingApiUrl;
            set => stagingApiUrl = value;
        }

        public string ProductionApiUrl
        {
            get => productionApiUrl;
            set => productionApiUrl = value;
        }

        public string ProjectApiKey
        {
            get => projectApiKey;
            set => projectApiKey = value;
        }

        public bool EnableDebugLogging
        {
            get => enableDebugLogging;
            set => enableDebugLogging = value;
        }

        public bool ValidateApiConnectivity
        {
            get => validateApiConnectivity;
            set => validateApiConnectivity = value;
        }

        /// <summary>
        /// Gets the current API URL based on the selected environment
        /// </summary>
        public string CurrentApiUrl
        {
            get
            {
                return environment switch
                {
                    EnvironmentType.Development => developmentApiUrl,
                    EnvironmentType.Staging => stagingApiUrl,
                    EnvironmentType.Production => productionApiUrl,
                    _ => developmentApiUrl
                };
            }
        }

        /// <summary>
        /// Loads the settings from Resources folder
        /// </summary>
        public static PeakSDKSettings LoadFromResources()
        {
            if (_instance == null)
            {
                _instance = Resources.Load<PeakSDKSettings>(ResourcesPath);

                if (_instance == null)
                {
                    Debug.LogWarning("[Peak SDK] Settings file not found. Please create one via 'Peak SDK > Settings' menu.");
                }
            }

            return _instance;
        }

        /// <summary>
        /// Creates SdkOptions from current settings
        /// </summary>
        public SdkOptions GetSdkOptions()
        {
            return new SdkOptions
            {
                ProjectApiKey = projectApiKey,
                ApiUrl = CurrentApiUrl
            };
        }

        /// <summary>
        /// Validates the current settings
        /// </summary>
        public ValidationResult ValidateSettings()
        {
            var result = new ValidationResult();

            // Validate API URL
            if (string.IsNullOrEmpty(CurrentApiUrl))
            {
                result.AddError("API URL is required");
            }
            else if (!Uri.TryCreate(CurrentApiUrl, UriKind.Absolute, out Uri uri))
            {
                result.AddError("API URL must be a valid URL");
            }
            else if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                result.AddError("API URL must use HTTP or HTTPS protocol");
            }

            // Validate API Key
            if (string.IsNullOrEmpty(projectApiKey))
            {
                result.AddError("Project API Key is required");
            }

            // Environment-specific validation
            ValidateEnvironmentConfiguration(result);

            return result;
        }

        /// <summary>
        /// Gets masked version of the API key for display purposes
        /// </summary>
        public string GetMaskedApiKey()
        {
            if (string.IsNullOrEmpty(projectApiKey))
                return "";

            const int showLast = 4;
            if (projectApiKey.Length <= showLast)
                return new string('*', projectApiKey.Length);

            var masked = new string('*', projectApiKey.Length - showLast);
            var visible = projectApiKey.Substring(projectApiKey.Length - showLast);
            return masked + visible;
        }

        /// <summary>
        /// Creates a default settings asset
        /// </summary>
        public static PeakSDKSettings CreateDefault()
        {
            var settings = CreateInstance<PeakSDKSettings>();
            settings.environment = EnvironmentType.Development;
            settings.enableDebugLogging = true;
            return settings;
        }

        /// <summary>
        /// Validates environment-specific configuration
        /// </summary>
        private void ValidateEnvironmentConfiguration(ValidationResult result)
        {
            // Check for production environment security
            if (environment == EnvironmentType.Production)
            {
                if (enableDebugLogging)
                {
                    result.AddWarning("Debug logging is enabled in production environment");
                }

                if (CurrentApiUrl.Contains("localhost") || CurrentApiUrl.Contains("127.0.0.1"))
                {
                    result.AddError("Production environment cannot use localhost URLs");
                }

                if (!CurrentApiUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    result.AddError("Production environment must use HTTPS");
                }
            }

            // Check for development environment warnings
            if (environment == EnvironmentType.Development)
            {
                if (!enableDebugLogging)
                {
                    result.AddWarning("Debug logging is disabled in development environment");
                }
            }

            // Validate individual API URLs
            ValidateUrl("Development", developmentApiUrl, result);
            ValidateUrl("Staging", stagingApiUrl, result);
            ValidateUrl("Production", productionApiUrl, result);
        }

        private void ValidateUrl(string envName, string url, ValidationResult result)
        {
            if (string.IsNullOrEmpty(url))
                return;

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                result.AddError($"{envName} API URL must be a valid URL");
            }
            else if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                result.AddError($"{envName} API URL must use HTTP or HTTPS protocol");
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Forces reload of settings in editor
        /// </summary>
        public static void ReloadSettings()
        {
            _instance = null;
        }
#endif
    }

    /// <summary>
    /// Validation result for settings
    /// </summary>
    [Serializable]
    public class ValidationResult
    {
        [SerializeField] private bool isValid = true;
        [SerializeField] private string[] errors = new string[0];
        [SerializeField] private string[] warnings = new string[0];

        public bool IsValid => isValid && errors.Length == 0;
        public string[] Errors => errors;
        public string[] Warnings => warnings;

        public void AddError(string error)
        {
            isValid = false;
            Array.Resize(ref errors, errors.Length + 1);
            errors[errors.Length - 1] = error;
        }

        public void AddWarning(string warning)
        {
            Array.Resize(ref warnings, warnings.Length + 1);
            warnings[warnings.Length - 1] = warning;
        }

        public string GetSummary()
        {
            if (IsValid && warnings.Length == 0)
                return "All settings are valid";

            var summary = "";
            if (errors.Length > 0)
                summary += $"{errors.Length} error(s)";
            if (warnings.Length > 0)
            {
                if (summary.Length > 0) summary += ", ";
                summary += $"{warnings.Length} warning(s)";
            }
            return summary;
        }
    }
}
