using System;
using System.Collections.Generic;

namespace KyuzanInc.Peak.Sdk
{
    /// <summary>
    /// Standardised string codes for <see cref="PeakError"/>.
    /// Semantic equivalent of the TS family's <c>PeakErrorCode</c> string enum.
    /// </summary>
    public static class PeakErrorCode
    {
        public const string InitializationFailed = "SDK_INITIALIZATION_FAILED";
        public const string AuthenticationFailed = "SDK_AUTHENTICATION_FAILED";
        public const string NotAuthenticated = "SDK_NOT_AUTHENTICATED";
        public const string SessionExpired = "SDK_SESSION_EXPIRED";
        public const string SessionStorageMissing = "SDK_SESSION_STORAGE_MISSING";
        public const string NetworkError = "SDK_NETWORK_ERROR";
        public const string HttpError = "SDK_HTTP_ERROR";
        public const string InvalidResponse = "SDK_INVALID_RESPONSE";
        public const string InvalidArgument = "SDK_INVALID_ARGUMENT";
        public const string InvalidJwt = "SDK_INVALID_JWT";
        public const string TurnkeyError = "SDK_TURNKEY_ERROR";
        public const string Unknown = "SDK_UNKNOWN";
    }

    /// <summary>
    /// Read-only context attached to a <see cref="PeakError"/> for log redaction.
    /// </summary>
    public sealed class ApiResponseContext
    {
        public int? HttpStatusCode { get; init; }
        public string? Endpoint { get; init; }
        public string? Method { get; init; }
        public string? RawResponseBody { get; init; }
    }

    /// <summary>
    /// Unified exception type for the Peak SDK. Semantic equivalent of the TS
    /// family's <c>PeakError</c> class. Carry a code (from
    /// <see cref="PeakErrorCode"/>) and optional response context.
    /// </summary>
    public sealed class PeakError : Exception
    {
        public string Code { get; }
        public ApiResponseContext? ApiResponse { get; }
        public IReadOnlyDictionary<string, object?> LogContext { get; }

        public PeakError(
            string code,
            string message,
            Exception? inner = null,
            ApiResponseContext? apiResponse = null,
            IReadOnlyDictionary<string, object?>? logContext = null)
            : base(message, inner)
        {
            Code = code ?? PeakErrorCode.Unknown;
            ApiResponse = apiResponse;
            LogContext = logContext ?? new Dictionary<string, object?>();
        }

        /// <summary>
        /// Wrap an arbitrary exception as a <see cref="PeakError"/>. If
        /// <paramref name="inner"/> already is a <see cref="PeakError"/>, it is
        /// returned unchanged (TS <c>toPeakError</c> semantics).
        /// </summary>
        public static PeakError From(Exception inner, string fallbackCode = PeakErrorCode.Unknown, string? fallbackMessage = null)
        {
            if (inner is PeakError already) return already;
            return new PeakError(fallbackCode, fallbackMessage ?? inner.Message, inner);
        }

        /// <summary>
        /// Helper that matches the TS <c>isPeakError</c> guard.
        /// </summary>
        public static bool IsAny(Exception? ex) => ex is PeakError;
    }
}
