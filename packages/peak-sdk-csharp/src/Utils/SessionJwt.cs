// Ported from upstream-snapshots/peak-sdk-unity/Runtime/Utils/SessionJwt.cs.
// Changes:
//   - namespace Peak.Utils -> KyuzanInc.Peak.Sdk.Utils
//   - Removed UnityEngine.Debug logging; failures throw PeakError instead.
//   - JsonUtility.FromJson -> JsonSerializer.Deserialize via PeakJsonContext.
//   - Exception types unified under PeakError (TokenExpired / NotAuthenticated
//     mapped to specific PeakErrorCode values).

using System;
using System.Text.Json;

namespace KyuzanInc.Peak.Sdk.Utils
{
    /// <summary>
    /// Session JWT payload structure. Aligned with <c>SessionJwtPayload</c>
    /// in <c>peak-sdk-browser/src/utils/session-jwt.ts</c>.
    /// </summary>
    public sealed class SessionJwtPayload
    {
        public string? SessionType { get; set; }
        public string? TurnkeyUserId { get; set; }
        public string? TurnkeySubOrgId { get; set; }
        public long Expiry { get; set; }
        public string? PublicKey { get; set; }
    }

    /// <summary>
    /// JSON shape inside the JWT payload (snake_case as Turnkey emits it).
    /// </summary>
    public sealed class SessionJwtPayloadJson
    {
        public long exp { get; set; }
        public string? organization_id { get; set; }
        public string? public_key { get; set; }
        public string? session_type { get; set; }
        public string? user_id { get; set; }
    }

    /// <summary>
    /// Verify, decode, and check expiry of Turnkey session JWTs.
    /// </summary>
    public static class SessionJwt
    {
        /// <summary>
        /// Verify the JWT's signature against the production notarizer key.
        /// </summary>
        public static bool VerifySessionJwt(string sessionJwt)
        {
            try
            {
                var ok = global::Turnkey.Crypto.VerifySessionJwtSignature(sessionJwt);
                if (!ok)
                {
                    throw new PeakError(PeakErrorCode.InvalidJwt, "Invalid JWT: failed signature verification");
                }
                return true;
            }
            catch (PeakError) { throw; }
            catch (Exception ex)
            {
                throw new PeakError(PeakErrorCode.InvalidJwt, "Invalid JWT: failed signature verification", ex);
            }
        }

        /// <summary>
        /// Decode the JWT payload. Does not verify the signature; pair with
        /// <see cref="VerifySessionJwt"/> when authenticity matters.
        /// </summary>
        public static SessionJwtPayload DecodeSessionJwt(string token)
        {
            var parts = token.Split('.');
            if (parts.Length < 2)
            {
                throw new PeakError(PeakErrorCode.InvalidJwt, "Invalid JWT: Missing payload");
            }

            string decoded;
            try
            {
                decoded = DecodeBase64Url(parts[1]);
            }
            catch (Exception ex)
            {
                throw new PeakError(PeakErrorCode.InvalidJwt, "Invalid JWT: Failed to decode payload", ex);
            }

            try
            {
                var obj = JsonSerializer.Deserialize(decoded, PeakJsonContext.Default.SessionJwtPayloadJson);
                if (obj is null
                    || obj.exp == 0
                    || string.IsNullOrEmpty(obj.organization_id)
                    || string.IsNullOrEmpty(obj.public_key)
                    || string.IsNullOrEmpty(obj.session_type)
                    || string.IsNullOrEmpty(obj.user_id))
                {
                    throw new PeakError(PeakErrorCode.InvalidJwt, "JWT payload missing required fields");
                }

                return new SessionJwtPayload
                {
                    SessionType = obj.session_type,
                    TurnkeyUserId = obj.user_id,
                    TurnkeySubOrgId = obj.organization_id,
                    Expiry = obj.exp,
                    PublicKey = obj.public_key,
                };
            }
            catch (PeakError) { throw; }
            catch (Exception ex)
            {
                throw new PeakError(PeakErrorCode.InvalidJwt, "Invalid JWT: Decoded payload is not valid JSON", ex);
            }
        }

        public static bool IsExpired(SessionJwtPayload payload)
        {
            return IsExpiredAt(payload, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        internal static bool IsExpiredAt(SessionJwtPayload payload, long nowUnixSeconds) =>
            payload.Expiry <= nowUnixSeconds;

        /// <summary>
        /// Verify signature + check expiry + verify public key match. Throws
        /// <see cref="PeakError"/> with code <c>SDK_SESSION_EXPIRED</c> or
        /// <c>SDK_INVALID_JWT</c> on failure.
        /// </summary>
        public static void ValidateSessionJwt(string sessionJwt, string? expectedPublicKey)
        {
            VerifySessionJwt(sessionJwt);

            var payload = DecodeSessionJwt(sessionJwt);
            if (IsExpired(payload))
            {
                throw new PeakError(PeakErrorCode.SessionExpired, "JWT has expired. Please login again.");
            }

            if (!string.IsNullOrEmpty(expectedPublicKey) && payload.PublicKey != expectedPublicKey)
            {
                throw new PeakError(PeakErrorCode.InvalidJwt,
                    "JWT public key does not match stored public key. Please login again.");
            }
        }

        private static string DecodeBase64Url(string input)
        {
            var output = input.Replace('-', '+').Replace('_', '/');
            switch (output.Length % 4)
            {
                case 2: output += "=="; break;
                case 3: output += "="; break;
            }
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(output));
        }
    }
}
