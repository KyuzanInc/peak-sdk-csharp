using System;
using UnityEngine;
using Peak.Exceptions;

namespace Peak.Utils
{
    /// <summary>
    /// Session JWT payload structure.
    /// Aligned with SessionJwtPayload in peak-sdk-browser/src/utils/session-jwt.ts
    /// </summary>
    public class SessionJwtPayload
    {
        /// <summary>
        /// Session type
        /// </summary>
        public string SessionType { get; set; }

        /// <summary>
        /// Turnkey user ID
        /// </summary>
        public string TurnkeyUserId { get; set; }

        /// <summary>
        /// Turnkey sub-organization ID
        /// </summary>
        public string TurnkeySubOrgId { get; set; }

        /// <summary>
        /// JWT expiry timestamp (Unix seconds)
        /// </summary>
        public long Expiry { get; set; }

        /// <summary>
        /// Public key associated with this session
        /// </summary>
        public string PublicKey { get; set; }
    }

    /// <summary>
    /// Utility class for session JWT verification and decoding.
    /// Aligned with session-jwt.ts in peak-sdk-browser.
    /// </summary>
    public static class SessionJwt
    {
        /// <summary>
        /// Verifies the signature of a session JWT.
        /// Equivalent to verifySessionJwt() in Browser SDK.
        /// </summary>
        /// <param name="sessionJwt">The JWT token to verify</param>
        /// <returns>True if the signature is valid</returns>
        /// <exception cref="Exception">Thrown if verification fails</exception>
        public static bool VerifySessionJwt(string sessionJwt)
        {
            try
            {
                var isValid = TurnkeyUtils.VerifySessionJwtSignature(sessionJwt);
                if (!isValid)
                {
                    throw new Exception("Invalid JWT: failed signature verification");
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"JWT signature verification failed: {ex.Message}");
                throw new Exception("Invalid JWT: failed signature verification", ex);
            }
        }

        /// <summary>
        /// Decodes a session JWT and extracts the payload.
        /// Equivalent to decodeSessionJwt() in Browser SDK.
        /// </summary>
        /// <param name="token">The JWT token to decode</param>
        /// <returns>The decoded payload</returns>
        /// <exception cref="Exception">Thrown if decoding fails</exception>
        public static SessionJwtPayload DecodeSessionJwt(string token)
        {
            var parts = token.Split('.');
            if (parts.Length < 2)
            {
                throw new Exception("Invalid JWT: Missing payload");
            }

            var payloadB64 = parts[1];
            string decoded;
            try
            {
                decoded = DecodeBase64Url(payloadB64);
            }
            catch (Exception ex)
            {
                Debug.LogError($"JWT payload decoding failed: {ex.Message}");
                throw new Exception("Invalid JWT: Failed to decode payload", ex);
            }

            try
            {
                var jsonObj = JsonUtility.FromJson<JwtPayloadJson>(decoded);

                // Validate required fields
                if (jsonObj.exp == 0 ||
                    string.IsNullOrEmpty(jsonObj.organization_id) ||
                    string.IsNullOrEmpty(jsonObj.public_key) ||
                    string.IsNullOrEmpty(jsonObj.session_type) ||
                    string.IsNullOrEmpty(jsonObj.user_id))
                {
                    throw new Exception("JWT payload missing required fields");
                }

                return new SessionJwtPayload
                {
                    SessionType = jsonObj.session_type,
                    TurnkeyUserId = jsonObj.user_id,
                    TurnkeySubOrgId = jsonObj.organization_id,
                    Expiry = jsonObj.exp,
                    PublicKey = jsonObj.public_key
                };
            }
            catch (Exception ex) when (!(ex.Message.Contains("JWT payload missing required fields")))
            {
                throw new Exception("Invalid JWT: Decoded payload is not valid JSON", ex);
            }
        }

        /// <summary>
        /// Checks if a session JWT has expired.
        /// </summary>
        /// <param name="payload">The decoded JWT payload</param>
        /// <returns>True if the JWT has expired</returns>
        public static bool IsExpired(SessionJwtPayload payload)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return payload.Expiry < now;
        }

        /// <summary>
        /// Validates a session JWT including signature, expiry, and public key match.
        /// </summary>
        /// <param name="sessionJwt">The JWT token to validate</param>
        /// <param name="expectedPublicKey">The expected public key to match</param>
        /// <exception cref="NotAuthenticatedException">Thrown when auth data is missing</exception>
        /// <exception cref="TokenExpiredException">Thrown when JWT has expired</exception>
        /// <exception cref="Exception">Thrown for other validation failures</exception>
        public static void ValidateSessionJwt(string sessionJwt, string expectedPublicKey)
        {
            // 1. Verify signature
            VerifySessionJwt(sessionJwt);

            // 2. Decode and check expiry
            var payload = DecodeSessionJwt(sessionJwt);
            if (IsExpired(payload))
            {
                throw new TokenExpiredException("JWT has expired. Please login again.");
            }

            // 3. Verify public key matches
            if (!string.IsNullOrEmpty(expectedPublicKey) && payload.PublicKey != expectedPublicKey)
            {
                throw new Exception("JWT public key does not match stored public key. Please login again.");
            }
        }

        private static string DecodeBase64Url(string input)
        {
            // Replace URL-safe characters
            var output = input.Replace('-', '+').Replace('_', '/');
            // Add padding
            switch (output.Length % 4)
            {
                case 2: output += "=="; break;
                case 3: output += "="; break;
            }
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(output));
        }

        /// <summary>
        /// Internal class for JSON deserialization
        /// </summary>
        [Serializable]
        private class JwtPayloadJson
        {
            public long exp;
            public string organization_id;
            public string public_key;
            public string session_type;
            public string user_id;
        }
    }
}
