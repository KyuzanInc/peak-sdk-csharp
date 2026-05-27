using System;
using Peak.Exceptions;
using UnityEngine;

namespace Peak.Utils
{
    /// <summary>
    /// Utility class for validating SDK configuration
    /// </summary>
    public static class ConfigValidator
    {
        public static void ValidateInitializationOptions(SdkOptions options)
        {
            if (options == null)
            {
                throw new SdkException(SdkErrorCodes.INVALID_CONFIG, "SdkOptions cannot be null");
            }

            if (string.IsNullOrEmpty(options.ProjectApiKey))
            {
                throw new SdkException(SdkErrorCodes.INVALID_CONFIG, "ProjectApiKey is required");
            }

            if (string.IsNullOrEmpty(options.ApiUrl))
            {
                throw new SdkException(SdkErrorCodes.INVALID_CONFIG, "ApiUrl is required");
            }

            if (!Uri.TryCreate(options.ApiUrl, UriKind.Absolute, out Uri uri))
            {
                throw new SdkException(SdkErrorCodes.INVALID_CONFIG, "ApiUrl must be a valid URL");
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new SdkException(SdkErrorCodes.INVALID_CONFIG, "ApiUrl must use HTTP or HTTPS protocol");
            }

            if (!PlatformHelper.IsMobilePlatform())
            {
                Debug.LogWarning("Peak SDK is optimized for mobile platforms. Current platform: " + PlatformHelper.GetPlatformName());
            }
        }
    }
}
