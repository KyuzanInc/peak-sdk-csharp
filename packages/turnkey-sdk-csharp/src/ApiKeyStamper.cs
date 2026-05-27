// Ported from upstream-snapshots/turnkey-sdk-unity/Runtime/ApiKeyStamper.cs (commit 039d8e4).
// Changes vs the Unity source:
//   - namespace Turnkey -> namespace KyuzanInc.Turnkey.Sdk
//   - removed `using UnityEngine;`
//   - replaced `JsonUtility.ToJson(stamp)` with System.Text.Json source-gen serialisation
//     (TurnkeyJsonContext.Default.TurnkeyStamp). The output is byte-equivalent JSON.
//   - UnityConstants -> CryptoConstants
//
// Logical equivalent of @turnkey/api-key-stamper v0.6.0.

using System;
using System.Text.Json;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.EC;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace KyuzanInc.Turnkey.Sdk
{
    /// <summary>
    /// Signs API requests with Turnkey API keys.
    /// Ported from @turnkey/api-key-stamper v0.6.0.
    /// Note: only DER signature format is supported. Raw format is not implemented.
    /// </summary>
    public class ApiKeyStamper
    {
        private const string STAMP_HEADER_NAME = "X-Stamp";
        private const string SIGNATURE_SCHEME = "SIGNATURE_SCHEME_TK_API_P256";

        private readonly string apiPublicKey;
        private readonly string apiPrivateKey;
        private readonly ECPrivateKeyParameters privateKeyParams;

        /// <summary>
        /// Initialise an API key stamper.
        /// </summary>
        /// <param name="apiPublicKey">API public key in hex format.</param>
        /// <param name="apiPrivateKey">API private key in hex format.</param>
        public ApiKeyStamper(string apiPublicKey, string apiPrivateKey)
        {
            this.apiPublicKey = apiPublicKey;
            this.apiPrivateKey = apiPrivateKey;

            // Initialise private key parameters
            var curve = ECNamedCurveTable.GetByName(CryptoConstants.CURVE_NAME);
            var domainParams = new ECDomainParameters(
                curve.Curve,
                curve.G,
                curve.N,
                curve.H,
                curve.GetSeed());

            var privateKeyBytes = Encoding.Uint8ArrayFromHexString(apiPrivateKey);

            // Ensure private key is exactly 32 bytes (pad with leading zeros if necessary)
            if (privateKeyBytes.Length < 32)
            {
                var paddedBytes = new byte[32];
                Array.Copy(privateKeyBytes, 0, paddedBytes, 32 - privateKeyBytes.Length, privateKeyBytes.Length);
                privateKeyBytes = paddedBytes;
            }
            else if (privateKeyBytes.Length > 32)
            {
                // If longer than 32 bytes, take only the last 32 bytes
                var truncatedBytes = new byte[32];
                Array.Copy(privateKeyBytes, privateKeyBytes.Length - 32, truncatedBytes, 0, 32);
                privateKeyBytes = truncatedBytes;
            }

            var d = new BigInteger(1, privateKeyBytes);

            // Validate that the private key is in valid range [1, n-1]
            if (d.CompareTo(BigInteger.One) < 0 || d.CompareTo(domainParams.N) >= 0)
            {
                throw new ArgumentException("Private key is out of valid range");
            }

            this.privateKeyParams = new ECPrivateKeyParameters(d, domainParams);
        }

        /// <summary>
        /// Result of stamping a payload.
        /// </summary>
        public class StampResult
        {
            public string? StampHeaderName { get; set; }
            public string? StampHeaderValue { get; set; }
        }

        /// <summary>
        /// Stamp envelope as JSON-serialised onto the X-Stamp header (base64url-encoded).
        /// Serialised via TurnkeyJsonContext for AOT / IL2CPP safety.
        /// Internal: visible to the serialiser and to tests, but not to consumers.
        /// </summary>
        internal class TurnkeyStamp
        {
            public string? PublicKey { get; set; }
            public string? Scheme { get; set; }
            public string? Signature { get; set; }
        }

        /// <summary>
        /// Create a signature stamp for the given payload.
        /// </summary>
        public StampResult Stamp(string payload)
        {
            var signature = SignPayload(payload);

            var stamp = new TurnkeyStamp
            {
                PublicKey = apiPublicKey,
                Scheme = SIGNATURE_SCHEME,
                Signature = signature,
            };

            // Source-generated, AOT-safe serialisation.
            var stampJson = JsonSerializer.Serialize(stamp, TurnkeyJsonContext.Default.TurnkeyStamp);

            return new StampResult
            {
                StampHeaderName = STAMP_HEADER_NAME,
                StampHeaderValue = Base64UrlEncode(stampJson),
            };
        }

        /// <summary>
        /// Sign payload using P-256 ECDSA with deterministic k (RFC 6979).
        /// Enforces low-S (BIP-62) for compatibility with noble/curves lowS:true.
        /// </summary>
        public string SignPayload(string payload)
        {
            // Verify that the derived public key matches the provided public key
            var derivedPublicKeyBytes = Crypto.GetPublicKey(apiPrivateKey, true);
            var derivedPublicKey = Encoding.Uint8ArrayToHexString(derivedPublicKeyBytes);
            if (!string.Equals(derivedPublicKey, apiPublicKey, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Provided public key doesn't match the private key");
            }

            // Convert payload to bytes
            var payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);

            // Create signer using HMacDsaKCalculator for deterministic signatures (RFC 6979)
            var hmacCalculator = new HMacDsaKCalculator(DigestUtilities.GetDigest("SHA-256"));
            var signer = new ECDsaSigner(hmacCalculator);
            signer.Init(true, privateKeyParams);

            // Hash the payload using SHA256
            var digest = DigestUtilities.GetDigest("SHA-256");
            digest.BlockUpdate(payloadBytes, 0, payloadBytes.Length);
            var hash = new byte[digest.GetDigestSize()];
            digest.DoFinal(hash, 0);

            // Sign the hash
            var signature = signer.GenerateSignature(hash);

            // Get r and s values
            var r = signature[0];
            var s = signature[1];

            // Ensure low s value (BIP 62) — noble/curves uses lowS: true by default
            var curve = ECNamedCurveTable.GetByName(CryptoConstants.CURVE_NAME);
            var n = curve.N;
            var halfN = n.ShiftRight(1);

            if (s.CompareTo(halfN) > 0)
            {
                s = n.Subtract(s);
            }

            // Convert to byte arrays
            var rBytes = r.ToByteArrayUnsigned();
            var sBytes = s.ToByteArrayUnsigned();

            // DER encoding: SEQUENCE { INTEGER r, INTEGER s }
            using (var ms = new System.IO.MemoryStream())
            {
                ms.WriteByte(0x30);

                int rLength = rBytes.Length;
                int sLength = sBytes.Length;

                bool rNeedsPadding = rBytes.Length > 0 && (rBytes[0] & 0x80) != 0;
                bool sNeedsPadding = sBytes.Length > 0 && (sBytes[0] & 0x80) != 0;

                if (rNeedsPadding) rLength++;
                if (sNeedsPadding) sLength++;

                int totalLength = 2 + rLength + 2 + sLength;
                ms.WriteByte((byte)totalLength);

                // Write R as INTEGER
                ms.WriteByte(0x02);
                ms.WriteByte((byte)rLength);
                if (rNeedsPadding) ms.WriteByte(0x00);
                ms.Write(rBytes, 0, rBytes.Length);

                // Write S as INTEGER
                ms.WriteByte(0x02);
                ms.WriteByte((byte)sLength);
                if (sNeedsPadding) ms.WriteByte(0x00);
                ms.Write(sBytes, 0, sBytes.Length);

                return Encoding.Uint8ArrayToHexString(ms.ToArray());
            }
        }

        /// <summary>
        /// Base64URL encode a string.
        /// </summary>
        private static string Base64UrlEncode(string input)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            var base64 = Convert.ToBase64String(bytes);

            return base64
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }
}
