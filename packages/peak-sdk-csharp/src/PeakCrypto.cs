// Public, Peak-owned wrapper over the client-side import/export crypto that
// the Turnkey port (global::Turnkey.Crypto / global::Turnkey.Encoding) exposes.
//
// Why this exists:
//   The wallet import/export flow needs client-side crypto primitives (derive
//   a P-256 public key, generate a target key pair, build an import bundle,
//   decrypt an export bundle), which until now were only reachable by calling
//   KyuzanInc.Turnkey.Sdk directly — forcing every consumer to take a direct
//   dependency on the Turnkey port. PeakCrypto re-exposes exactly that slice on
//   the Peak surface with Peak-owned param/result types, so consumers depend
//   only on KyuzanInc.Peak.Sdk and never reference Turnkey.* directly.
//
// Design:
//   - THIN delegation. Every method forwards to global::Turnkey.* and maps the
//     Peak param/result types onto the Turnkey ones field-by-field. No crypto
//     logic lives here; the bytes are decided entirely by the Turnkey port.
//   - The Peak param types intentionally OMIT Turnkey's
//     DangerouslyOverrideSignerPublicKey: production consumers must verify
//     against the real Turnkey signer/notarizer, so the test-only override is
//     not part of the public Peak surface.
//   - DLL-level internalization of Turnkey is out of scope; Turnkey remains a
//     normal transitive dependency. This wrapper is purely an API-surface
//     concern so consumers get a Turnkey-free import.

using System;
using TurnkeyCrypto = global::Turnkey.Crypto;
using TurnkeyEncoding = global::Turnkey.Encoding;

namespace KyuzanInc.Peak.Sdk
{
    /// <summary>
    /// Public, Peak-owned facade over the client-side import/export crypto from
    /// <c>KyuzanInc.Turnkey.Sdk</c>. Thin delegation: every member forwards to
    /// <c>global::Turnkey.Crypto</c> / <c>global::Turnkey.Encoding</c> and maps the
    /// Peak-owned parameter/result types onto the Turnkey ones so consumers never
    /// reference <c>Turnkey.*</c> directly.
    /// </summary>
    public static class PeakCrypto
    {
        /// <summary>
        /// A P-256 key pair (hex-encoded) as returned by
        /// <see cref="GenerateP256KeyPair"/>. Peak-owned mirror of
        /// <c>global::Turnkey.Crypto.KeyPair</c>.
        /// </summary>
        public sealed class KeyPair
        {
            /// <summary>32-byte private key, lower-case hex (64 chars).</summary>
            public string PrivateKey { get; init; } = string.Empty;

            /// <summary>33-byte compressed SEC1 public key, lower-case hex (66 chars).</summary>
            public string PublicKey { get; init; } = string.Empty;

            /// <summary>65-byte uncompressed SEC1 public key, lower-case hex (130 chars).</summary>
            public string PublicKeyUncompressed { get; init; } = string.Empty;
        }

        /// <summary>
        /// Parameters for <see cref="EncryptPrivateKeyToBundle"/>. Peak-owned mirror
        /// of <c>global::Turnkey.Crypto.EncryptPrivateKeyToBundleParams</c>.
        /// Deliberately omits the test-only signer override.
        /// </summary>
        public sealed class EncryptPrivateKeyToBundleParams
        {
            /// <summary>The private key to import, encoded per <see cref="KeyFormat"/>.</summary>
            public string? PrivateKey { get; init; }

            /// <summary>The Turnkey import bundle JSON returned by the init-import activity.</summary>
            public string? ImportBundle { get; init; }

            /// <summary>The organization id that must match the bundle's signed data.</summary>
            public string? OrganizationId { get; init; }

            /// <summary>The user id that must match the bundle's signed data.</summary>
            public string? UserId { get; init; }

            /// <summary>
            /// Key encoding of <see cref="PrivateKey"/> (e.g. <c>"HEXADECIMAL"</c> or
            /// <c>"SOLANA"</c>). Forwarded as-is; the underlying Turnkey call treats an
            /// unset value as hex.
            /// </summary>
            public string? KeyFormat { get; init; }
        }

        /// <summary>
        /// Parameters for <see cref="DecryptExportBundle"/>. Peak-owned mirror of
        /// <c>global::Turnkey.Crypto.DecryptExportBundleParams</c>. Deliberately
        /// omits the test-only signer override.
        /// </summary>
        public sealed class DecryptExportBundleParams
        {
            /// <summary>The Turnkey export bundle JSON returned by the export activity.</summary>
            public string? ExportBundle { get; init; }

            /// <summary>The embedded (target) private key, hex-encoded, used to decrypt.</summary>
            public string? EmbeddedKey { get; init; }

            /// <summary>The organization id that must match the bundle's signed data.</summary>
            public string? OrganizationId { get; init; }

            /// <summary>
            /// Key encoding hint for the decrypted output (e.g. <c>"HEXADECIMAL"</c>
            /// or <c>"SOLANA"</c>). Forwarded as-is; the underlying Turnkey call treats
            /// an unset value as hex.
            /// </summary>
            public string? KeyFormat { get; init; }

            /// <summary>
            /// When <c>true</c>, return the decrypted material decoded as a BIP-39
            /// mnemonic; otherwise return it as hex (or base58 for <c>"SOLANA"</c>).
            /// </summary>
            public bool ReturnMnemonic { get; init; }
        }

        /// <summary>
        /// Derive the SEC1 public key bytes from a 32-byte P-256 private key.
        /// Delegates to <c>global::Turnkey.Crypto.GetPublicKey</c>.
        /// </summary>
        /// <param name="privateKey">The 32-byte private key.</param>
        /// <param name="isCompressed">
        /// When <c>true</c> (default) returns the 33-byte compressed form; otherwise
        /// the 65-byte uncompressed form.
        /// </param>
        /// <returns>
        /// The SEC1 public key bytes: 33 bytes when <paramref name="isCompressed"/> is
        /// <c>true</c>, otherwise 65 bytes.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown by the underlying Turnkey call when <paramref name="privateKey"/> is null.
        /// </exception>
        public static byte[] GetPublicKey(byte[] privateKey, bool isCompressed = true) =>
            TurnkeyCrypto.GetPublicKey(privateKey, isCompressed);

        /// <summary>
        /// Generate a random P-256 key pair (used as the import/export target key).
        /// Delegates to <c>global::Turnkey.Crypto.GenerateP256KeyPair</c> and maps
        /// the result onto <see cref="KeyPair"/>.
        /// </summary>
        /// <returns>A <see cref="KeyPair"/> with all three hex fields populated.</returns>
        public static KeyPair GenerateP256KeyPair()
        {
            var kp = TurnkeyCrypto.GenerateP256KeyPair();

            // Map field-by-field. The Turnkey KeyPair properties are nullable-oblivious
            // strings, so the non-nullable Peak KeyPair contract is only honest if we
            // fail fast on an (unexpected) null rather than silently exposing one.
            return new KeyPair
            {
                PrivateKey = kp.PrivateKey ?? throw new InvalidOperationException(
                    "Turnkey.Crypto.GenerateP256KeyPair returned a null PrivateKey."),
                PublicKey = kp.PublicKey ?? throw new InvalidOperationException(
                    "Turnkey.Crypto.GenerateP256KeyPair returned a null PublicKey."),
                PublicKeyUncompressed = kp.PublicKeyUncompressed ?? throw new InvalidOperationException(
                    "Turnkey.Crypto.GenerateP256KeyPair returned a null PublicKeyUncompressed."),
            };
        }

        /// <summary>
        /// Encrypt a private key into a Turnkey import bundle.
        /// Delegates to <c>global::Turnkey.Crypto.EncryptPrivateKeyToBundle</c>,
        /// mapping <see cref="EncryptPrivateKeyToBundleParams"/> onto the Turnkey
        /// params field-by-field (the signer override is never forwarded).
        /// </summary>
        /// <param name="parameters">The import parameters.</param>
        /// <returns>The import-bundle JSON envelope to send to Turnkey.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="parameters"/> is null.
        /// </exception>
        public static string EncryptPrivateKeyToBundle(EncryptPrivateKeyToBundleParams parameters)
        {
            if (parameters is null) throw new ArgumentNullException(nameof(parameters));

            return TurnkeyCrypto.EncryptPrivateKeyToBundle(
                new TurnkeyCrypto.EncryptPrivateKeyToBundleParams
                {
                    PrivateKey = parameters.PrivateKey,
                    ImportBundle = parameters.ImportBundle,
                    OrganizationId = parameters.OrganizationId,
                    UserId = parameters.UserId,
                    KeyFormat = parameters.KeyFormat,
                });
        }

        /// <summary>
        /// Decrypt a Turnkey export bundle.
        /// Delegates to <c>global::Turnkey.Crypto.DecryptExportBundle</c>, mapping
        /// <see cref="DecryptExportBundleParams"/> onto the Turnkey params
        /// field-by-field (the signer override is never forwarded).
        /// </summary>
        /// <param name="parameters">The export parameters.</param>
        /// <returns>
        /// The decrypted private key (hex), mnemonic, or base58 (SOLANA), depending
        /// on <see cref="DecryptExportBundleParams.KeyFormat"/> and
        /// <see cref="DecryptExportBundleParams.ReturnMnemonic"/>.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when <paramref name="parameters"/> is null.
        /// </exception>
        public static string DecryptExportBundle(DecryptExportBundleParams parameters)
        {
            if (parameters is null) throw new ArgumentNullException(nameof(parameters));

            return TurnkeyCrypto.DecryptExportBundle(
                new TurnkeyCrypto.DecryptExportBundleParams
                {
                    ExportBundle = parameters.ExportBundle,
                    EmbeddedKey = parameters.EmbeddedKey,
                    OrganizationId = parameters.OrganizationId,
                    KeyFormat = parameters.KeyFormat,
                    ReturnMnemonic = parameters.ReturnMnemonic,
                });
        }

        /// <summary>
        /// Convert a byte array into a lower-case hex string.
        /// Delegates to <c>global::Turnkey.Encoding.Uint8ArrayToHexString</c>.
        /// </summary>
        /// <param name="input">The bytes to encode. An empty array yields an empty string.</param>
        /// <returns>The lower-case hex encoding of <paramref name="input"/>.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown by the underlying Turnkey call when <paramref name="input"/> is null.
        /// </exception>
        public static string Uint8ArrayToHexString(byte[] input) =>
            TurnkeyEncoding.Uint8ArrayToHexString(input);

        /// <summary>
        /// Create a byte array from a hex string.
        /// Delegates to <c>global::Turnkey.Encoding.Uint8ArrayFromHexString</c>.
        /// </summary>
        /// <param name="hexString">Even-length hex string containing only <c>[0-9A-Fa-f]</c>.</param>
        /// <param name="length">
        /// Optional target length; when set the result is left-padded with leading
        /// zero bytes (or an exception is thrown if the value does not fit).
        /// </param>
        /// <returns>The decoded bytes, left-padded to <paramref name="length"/> when set.</returns>
        /// <exception cref="System.ArgumentException">
        /// Thrown by the underlying Turnkey call when <paramref name="hexString"/> is
        /// null, empty, odd-length, contains non-hex characters, or does not fit in
        /// <paramref name="length"/> bytes. Note this is <c>ArgumentException</c> — not
        /// <c>ArgumentNullException</c> — even for a null input.
        /// </exception>
        public static byte[] Uint8ArrayFromHexString(string hexString, int? length = null) =>
            TurnkeyEncoding.Uint8ArrayFromHexString(hexString, length);
    }
}
