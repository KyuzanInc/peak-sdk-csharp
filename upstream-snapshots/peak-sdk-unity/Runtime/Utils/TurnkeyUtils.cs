using Turnkey;

namespace Peak.Utils
{
    /// <summary>
    /// Turnkey integration utilities for Peak SDK.
    /// Wraps the external turnkey-sdk-unity package and provides a consistent API
    /// similar to @packages/peak-sdk-node/src/utils/turnkey.ts
    /// </summary>
    /// <remarks>
    /// <para><b>WHY "TurnkeyUtils" INSTEAD OF "Turnkey":</b></para>
    /// <para>
    /// The class is named "TurnkeyUtils" rather than "Turnkey" to avoid a naming conflict
    /// with the external "Turnkey" namespace from turnkey-sdk-unity package.
    /// In C#, when both a namespace and a type share the same name, the compiler
    /// prioritizes the namespace, causing resolution errors. Using "TurnkeyUtils"
    /// ensures unambiguous access throughout the codebase.
    /// </para>
    ///
    /// <para><b>RE-EXPORTED TYPES (use via TurnkeyUtils.*):</b></para>
    /// <list type="bullet">
    ///   <item><description>P256KeyPair - Key pair with implicit conversion from Turnkey.Crypto.KeyPair</description></item>
    ///   <item><description>EncryptPrivateKeyToBundleParams - Parameters for client-side encryption</description></item>
    ///   <item><description>DecryptExportBundleParams - Parameters for client-side decryption</description></item>
    ///   <item><description>*RequestBody, *Parameters - Request DTOs for Turnkey API calls (internal use)</description></item>
    /// </list>
    ///
    /// <para><b>NOT RE-EXPORTED (use via global::Turnkey.*):</b></para>
    /// <list type="bullet">
    ///   <item><description>
    ///     Http, SignedRequest, Stamp - These are returned by Turnkey.Http methods.
    ///     C# does not allow implicit conversion from base class to derived class,
    ///     so re-exporting would cause type mismatch errors.
    ///     Use global::Turnkey.Http and global::Turnkey.Http.SignedRequest directly.
    ///   </description></item>
    /// </list>
    /// </remarks>
    public static class TurnkeyUtils
    {
        // ============================================================
        // Re-exported Types
        // ============================================================
        // These types are re-exported for SDK users to access via TurnkeyUtils.*
        // They either have implicit conversion or are only used as input parameters.

        /// <summary>
        /// P256 key pair structure with implicit conversion from Turnkey.Crypto.KeyPair.
        /// </summary>
        public class P256KeyPair
        {
            public string privateKey { get; set; }
            public string publicKey { get; set; }
            public string publicKeyUncompressed { get; set; }

            public static implicit operator P256KeyPair(Crypto.KeyPair keyPair)
            {
                return new P256KeyPair
                {
                    privateKey = keyPair.PrivateKey,
                    publicKey = keyPair.PublicKey,
                    publicKeyUncompressed = keyPair.PublicKeyUncompressed
                };
            }
        }

        /// <summary>
        /// Parameters for encrypting private key to bundle.
        /// Used as input to <see cref="EncryptPrivateKeyToBundle"/>.
        /// </summary>
        public class EncryptPrivateKeyToBundleParams : Crypto.EncryptPrivateKeyToBundleParams { }

        /// <summary>
        /// Parameters for decrypting export bundle.
        /// Used as input to <see cref="DecryptExportBundle"/>.
        /// </summary>
        public class DecryptExportBundleParams : Crypto.DecryptExportBundleParams { }

        // ============================================================
        // Internal Request Types (re-exported for SDK internal use)
        // ============================================================
        // These are used internally by PrivateKeyService to build Turnkey API requests.
        // They are input-only types, so re-exporting works without type conversion issues.

        /// <summary>Init import private key request body.</summary>
        public class InitImportPrivateKeyRequestBody : Http.InitImportPrivateKeyRequestBody { }

        /// <summary>Init import private key parameters.</summary>
        public class InitImportPrivateKeyParameters : Http.InitImportPrivateKeyParameters { }

        /// <summary>Import private key request body.</summary>
        public class ImportPrivateKeyRequestBody : Http.ImportPrivateKeyRequestBody { }

        /// <summary>Import private key parameters.</summary>
        public class ImportPrivateKeyParameters : Http.ImportPrivateKeyParameters { }

        /// <summary>Export private key request body.</summary>
        public class ExportPrivateKeyRequestBody : Http.ExportPrivateKeyRequestBody { }

        /// <summary>Export private key parameters.</summary>
        public class ExportPrivateKeyParameters : Http.ExportPrivateKeyParameters { }

        /// <summary>Export wallet account request body.</summary>
        public class ExportWalletAccountRequestBody : Http.ExportWalletAccountRequestBody { }

        /// <summary>Export wallet account parameters.</summary>
        public class ExportWalletAccountParameters : Http.ExportWalletAccountParameters { }

        // ============================================================
        // NOT Re-exported: Http, SignedRequest, Stamp
        // ============================================================
        // These types are returned by global::Turnkey.Http methods.
        // C# does not allow implicit conversion from base class (Turnkey.Http.SignedRequest)
        // to derived class (TurnkeyUtils.SignedRequest), so if we re-exported them,
        // code like this would fail:
        //   TurnkeyUtils.SignedRequest req = turnkeyClient.StampExportPrivateKey(body);
        //                                   ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
        //                                   Returns global::Turnkey.Http.SignedRequest
        // Use global::Turnkey.Http and global::Turnkey.Http.SignedRequest directly.

        // ============================================================
        // Key Generation
        // ============================================================

        /// <summary>
        /// Generate P256 key pair.
        /// Equivalent to generateP256KeyPair() in Node.js SDK.
        /// </summary>
        public static P256KeyPair GenerateP256KeyPair()
        {
            return Crypto.GenerateP256KeyPair();
        }

        /// <summary>
        /// Get public key from private key bytes.
        /// </summary>
        /// <param name="privateKey">Private key bytes.</param>
        /// <param name="compressed">Whether to return compressed format.</param>
        /// <returns>Public key bytes.</returns>
        public static byte[] GetPublicKey(byte[] privateKey, bool compressed)
        {
            return Crypto.GetPublicKey(privateKey, compressed);
        }

        // ============================================================
        // HTTP Client
        // ============================================================

        /// <summary>
        /// Get Turnkey HTTP client from target private key.
        /// Equivalent to getTurnkeyHttpClient() in Node.js SDK.
        /// </summary>
        /// <param name="targetPrivateKey">Target private key for session JWT authentication.</param>
        /// <returns>Turnkey HTTP client instance.</returns>
        public static Http GetTurnkeyHttpClient(string targetPrivateKey)
        {
            return Http.FromTargetPrivateKey(targetPrivateKey);
        }

        // ============================================================
        // Import / Export
        // ============================================================

        /// <summary>
        /// Encrypt private key to bundle for Turnkey import.
        /// Equivalent to encryptPrivateKeyToBundle() in Node.js SDK.
        /// </summary>
        /// <param name="parameters">Encryption parameters.</param>
        /// <returns>Encrypted bundle string.</returns>
        public static string EncryptPrivateKeyToBundle(EncryptPrivateKeyToBundleParams parameters)
        {
            var cryptoParams = new Crypto.EncryptPrivateKeyToBundleParams
            {
                privateKey = parameters.privateKey,
                importBundle = parameters.importBundle,
                organizationId = parameters.organizationId,
                userId = parameters.userId,
                keyFormat = parameters.keyFormat
            };
            return Crypto.EncryptPrivateKeyToBundle(cryptoParams);
        }

        /// <summary>
        /// Decrypt export bundle from Turnkey.
        /// Equivalent to decryptExportBundle() in Node.js SDK.
        /// </summary>
        /// <param name="parameters">Decryption parameters.</param>
        /// <returns>Decrypted private key string.</returns>
        public static string DecryptExportBundle(DecryptExportBundleParams parameters)
        {
            var cryptoParams = new Crypto.DecryptExportBundleParams
            {
                exportBundle = parameters.exportBundle,
                embeddedKey = parameters.embeddedKey,
                organizationId = parameters.organizationId,
                returnMnemonic = parameters.returnMnemonic,
                keyFormat = parameters.keyFormat
            };
            return Crypto.DecryptExportBundle(cryptoParams);
        }

        // ============================================================
        // JWT
        // ============================================================

        /// <summary>
        /// Verify session JWT signature.
        /// Equivalent to verifySessionJwtSignature() in Browser SDK.
        /// </summary>
        /// <param name="sessionJwt">Session JWT to verify.</param>
        /// <returns>True if signature is valid, false otherwise.</returns>
        public static bool VerifySessionJwtSignature(string sessionJwt)
        {
            return Crypto.VerifySessionJwtSignature(sessionJwt);
        }

        // ============================================================
        // Encoding
        // ============================================================

        /// <summary>
        /// Convert byte array to hex string.
        /// </summary>
        /// <param name="bytes">Byte array to convert.</param>
        /// <returns>Hex string representation.</returns>
        public static string Uint8ArrayToHexString(byte[] bytes)
        {
            return global::Turnkey.Encoding.Uint8ArrayToHexString(bytes);
        }

        /// <summary>
        /// Convert hex string to byte array.
        /// </summary>
        /// <param name="hex">Hex string to convert.</param>
        /// <returns>Byte array.</returns>
        public static byte[] Uint8ArrayFromHexString(string hex)
        {
            return global::Turnkey.Encoding.Uint8ArrayFromHexString(hex);
        }
    }
}
