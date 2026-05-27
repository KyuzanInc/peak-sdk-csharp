using System;

namespace Peak.Models.Api
{
    /// <summary>
    /// Request DTO for initiating private key import.
    /// </summary>
    [Serializable]
    public class InitImportPrivateKeyRequest
    {
        public global::Turnkey.Http.SignedRequest signedInitImportPrivateKeyRequest;

        public InitImportPrivateKeyRequest(global::Turnkey.Http.SignedRequest signedRequest)
        {
            signedInitImportPrivateKeyRequest = signedRequest;
        }
    }

    /// <summary>
    /// Request DTO for completing private key import.
    /// </summary>
    [Serializable]
    public class CompleteImportPrivateKeyRequest
    {
        /// <summary>
        /// The chain type for the imported private key.
        /// Supported values: "evm", "solana"
        /// </summary>
        public string chainType;

        /// <summary>
        /// The signed Turnkey request for completing the import.
        /// </summary>
        public global::Turnkey.Http.SignedRequest signedCompleteImportPrivateKeyRequest;

        public CompleteImportPrivateKeyRequest(string chainType, global::Turnkey.Http.SignedRequest signedRequest)
        {
            this.chainType = chainType;
            signedCompleteImportPrivateKeyRequest = signedRequest;
        }
    }

    /// <summary>
    /// Request DTO for exporting a private key.
    /// </summary>
    [Serializable]
    public class ExportPrivateKeyRequest
    {
        /// <summary>
        /// Account source type. Always "private-key" for this request.
        /// </summary>
        public string sourceType;

        /// <summary>
        /// Signed export request from Turnkey.
        /// </summary>
        public global::Turnkey.Http.SignedRequest signedExportPrivateKeyRequest;

        public ExportPrivateKeyRequest(string sourceType, global::Turnkey.Http.SignedRequest signedRequest)
        {
            this.sourceType = sourceType;
            this.signedExportPrivateKeyRequest = signedRequest;
        }
    }
}
