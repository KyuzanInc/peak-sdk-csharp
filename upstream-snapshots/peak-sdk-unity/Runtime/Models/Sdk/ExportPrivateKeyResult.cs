using System;

namespace Peak.Models.Sdk
{
    /// <summary>
    /// Result of exporting a private key.
    /// The export bundle is encrypted and must be decrypted client-side
    /// using TurnkeyUtils.DecryptExportBundle().
    /// </summary>
    [Serializable]
    public class ExportPrivateKeyResult
    {
        /// <summary>
        /// Encrypted private key export bundle from Turnkey.
        /// </summary>
        public string exportBundle;

        /// <summary>
        /// Turnkey organization ID needed for decryption.
        /// </summary>
        public string organizationId;
    }
}
