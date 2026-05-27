using System;

namespace Peak.Exceptions
{
    /// <summary>
    /// Base exception for Peak SDK errors.
    /// </summary>
    public class SdkException : Exception
    {
        public string ErrorCode { get; }

        public SdkException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }

        public SdkException(string errorCode, string message, Exception innerException) : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }

    /// <summary>
    /// Error codes for SDK exceptions.
    /// </summary>
    public static class SdkErrorCodes
    {
        public const string INVALID_CONFIG = "INVALID_CONFIG";
        public const string PLATFORM_NOT_SUPPORTED = "PLATFORM_NOT_SUPPORTED";
        public const string INITIALIZATION_FAILED = "INITIALIZATION_FAILED";
        public const string NETWORK_ERROR = "NETWORK_ERROR";
        public const string AUTHENTICATION_FAILED = "AUTHENTICATION_FAILED";
    }
}
