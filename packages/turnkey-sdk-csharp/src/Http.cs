// Ported from upstream-snapshots/turnkey-sdk-unity/Runtime/Http.cs (commit 039d8e4).
// Changes vs the Unity source:
//   - namespace Turnkey -> namespace KyuzanInc.Turnkey.Sdk
//   - removed `using UnityEngine;`
//   - replaced `JsonUtility.ToJson(body)` with System.Text.Json source-gen serialisation
//     via TurnkeyJsonContext. The output is byte-equivalent JSON.
//   - nested DTOs preserved per D16 (Komy directive: do not flatten to top-level types).
//   - `[Serializable]` attribute removed (was Unity-only, no behaviour);
//     DTOs use auto-properties so System.Text.Json source-gen can introspect them.
//
// Logical equivalent of @turnkey/http v3.16.0 (stamping subset only).

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace KyuzanInc.Turnkey.Sdk
{
    /// <summary>
    /// Creates signed Turnkey requests using API key stamping.
    /// Ported from @turnkey/http v3.16.0 (stamping subset).
    /// </summary>
    public class Http
    {
        private const string BaseUrl = "https://api.turnkey.com";

        private readonly ApiKeyStamper stamper;

        private Http(ApiKeyStamper stamper)
        {
            this.stamper = stamper ?? throw new ArgumentNullException(nameof(stamper));
        }

        /// <summary>
        /// Create client from encrypted credential bundle (legacy flow).
        /// </summary>
        public static Http GetHttpClient(string encryptedCredentialBundle, string targetPrivateKey)
        {
            if (string.IsNullOrEmpty(encryptedCredentialBundle))
            {
                throw new ArgumentException("Encrypted credential bundle is required", nameof(encryptedCredentialBundle));
            }

            if (string.IsNullOrEmpty(targetPrivateKey))
            {
                throw new ArgumentException("Target private key is required", nameof(targetPrivateKey));
            }

            var apiPrivateKey = Crypto.DecryptCredentialBundle(encryptedCredentialBundle, targetPrivateKey);
            var apiPrivateKeyBytes = Encoding.Uint8ArrayFromHexString(apiPrivateKey);
            var apiPublicKeyBytes = Crypto.GetPublicKey(apiPrivateKeyBytes, true);

            var normalizedPrivateKey = Encoding.Uint8ArrayToHexString(apiPrivateKeyBytes);
            var apiPublicKey = Encoding.Uint8ArrayToHexString(apiPublicKeyBytes);

            return new Http(new ApiKeyStamper(apiPublicKey, normalizedPrivateKey));
        }

        /// <summary>
        /// Create client directly from a target private key (OTP session flow).
        /// </summary>
        public static Http FromTargetPrivateKey(string targetPrivateKey)
        {
            if (string.IsNullOrWhiteSpace(targetPrivateKey))
            {
                throw new ArgumentException("Target private key is required", nameof(targetPrivateKey));
            }

            var privateKeyBytes = Encoding.Uint8ArrayFromHexString(targetPrivateKey);
            if (privateKeyBytes.Length == 0)
            {
                throw new ArgumentException("Target private key was not valid hex", nameof(targetPrivateKey));
            }

            var normalizedPrivateKey = Encoding.Uint8ArrayToHexString(privateKeyBytes);
            var publicKeyBytes = Crypto.GetPublicKey(privateKeyBytes, true);
            var publicKeyHex = Encoding.Uint8ArrayToHexString(publicKeyBytes);

            return new Http(new ApiKeyStamper(publicKeyHex, normalizedPrivateKey));
        }

        public SignedRequest StampGetWhoami(string organizationId)
        {
            if (string.IsNullOrEmpty(organizationId))
            {
                throw new ArgumentException("Organization ID is required", nameof(organizationId));
            }

            var body = new WhoamiRequestBody { OrganizationId = organizationId };
            return CreateSignedRequest($"{BaseUrl}/public/v1/query/whoami", body, TurnkeyJsonContext.Default.WhoamiRequestBody);
        }

        public SignedRequest StampInitImportPrivateKey(InitImportPrivateKeyRequestBody body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            return CreateSignedRequest($"{BaseUrl}/public/v1/submit/init_import_private_key", body, TurnkeyJsonContext.Default.InitImportPrivateKeyRequestBody);
        }

        public SignedRequest StampImportPrivateKey(ImportPrivateKeyRequestBody body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            return CreateSignedRequest($"{BaseUrl}/public/v1/submit/import_private_key", body, TurnkeyJsonContext.Default.ImportPrivateKeyRequestBody);
        }

        public SignedRequest StampExportPrivateKey(ExportPrivateKeyRequestBody body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            return CreateSignedRequest($"{BaseUrl}/public/v1/submit/export_private_key", body, TurnkeyJsonContext.Default.ExportPrivateKeyRequestBody);
        }

        public SignedRequest StampExportWalletAccount(ExportWalletAccountRequestBody body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            return CreateSignedRequest($"{BaseUrl}/public/v1/submit/export_wallet_account", body, TurnkeyJsonContext.Default.ExportWalletAccountRequestBody);
        }

        private SignedRequest CreateSignedRequest<TBody>(string url, TBody body, JsonTypeInfo<TBody> typeInfo) where TBody : class
        {
            var bodyJson = JsonSerializer.Serialize(body, typeInfo);
            var stampResult = stamper.Stamp(bodyJson);

            return new SignedRequest
            {
                Url = url,
                Body = bodyJson,
                Stamp = new Stamp
                {
                    StampHeaderName = stampResult.StampHeaderName,
                    StampHeaderValue = stampResult.StampHeaderValue,
                },
            };
        }

        #region DTOs (nested, per D16 — do not flatten to top-level)

        /// <summary>
        /// Signed request envelope.
        /// </summary>
        public class SignedRequest
        {
            public string? Url { get; set; }
            public string? Body { get; set; }
            public Stamp? Stamp { get; set; }
        }

        /// <summary>
        /// Stamp header pair.
        /// </summary>
        public class Stamp
        {
            public string? StampHeaderName { get; set; }
            public string? StampHeaderValue { get; set; }
        }

        public class WhoamiRequestBody
        {
            public string? OrganizationId { get; set; }
        }

        public class InitImportPrivateKeyRequestBody
        {
            public string? OrganizationId { get; set; }
            public string? Type { get; set; }
            public string? TimestampMs { get; set; }
            public InitImportPrivateKeyParameters? Parameters { get; set; }
        }

        public class InitImportPrivateKeyParameters
        {
            public string? UserId { get; set; }
        }

        public class ImportPrivateKeyRequestBody
        {
            public string? OrganizationId { get; set; }
            public string? Type { get; set; }
            public string? TimestampMs { get; set; }
            public ImportPrivateKeyParameters? Parameters { get; set; }
        }

        public class ImportPrivateKeyParameters
        {
            public string? UserId { get; set; }
            public string[]? AddressFormats { get; set; }
            public string? Curve { get; set; }
            public string? EncryptedBundle { get; set; }
            public string? PrivateKeyName { get; set; }
        }

        public class ExportPrivateKeyRequestBody
        {
            public string? OrganizationId { get; set; }
            public string? Type { get; set; }
            public string? TimestampMs { get; set; }
            public ExportPrivateKeyParameters? Parameters { get; set; }
        }

        public class ExportPrivateKeyParameters
        {
            public string? PrivateKeyId { get; set; }
            public string? TargetPublicKey { get; set; }
        }

        public class ExportWalletAccountRequestBody
        {
            public string? OrganizationId { get; set; }
            public string? Type { get; set; }
            public string? TimestampMs { get; set; }
            public ExportWalletAccountParameters? Parameters { get; set; }
        }

        public class ExportWalletAccountParameters
        {
            public string? Address { get; set; }
            public string? TargetPublicKey { get; set; }
        }

        #endregion
    }
}
