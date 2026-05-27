using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Peak.Models.Api;
using Peak.Models.Sdk;
using Peak.Services;
using Peak.Utils;
using UnityEngine;

namespace Peak.Services.Authenticated
{
    /// <summary>
    /// Service for private key import/export operations.
    /// Aligned with PrivateKeyService in peak-sdk-browser.
    /// </summary>
    public class PrivateKeyService
    {
        private readonly IPeakHttpClient httpClient;
        private readonly string sessionJwt;
        private readonly string targetPrivateKey;

        public PrivateKeyService(string apiUrl, string projectApiKey, string sessionJwt, string targetPrivateKey, IPeakHttpClient httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(sessionJwt))
            {
                throw new ArgumentException("Session JWT is required", nameof(sessionJwt));
            }

            if (string.IsNullOrWhiteSpace(targetPrivateKey))
            {
                throw new ArgumentException("Target private key is required", nameof(targetPrivateKey));
            }

            this.httpClient = httpClient ?? new PeakHttpClient(apiUrl, projectApiKey);
            this.sessionJwt = sessionJwt;
            this.targetPrivateKey = targetPrivateKey;
        }

        private Dictionary<string, string> CreateAuthHeaders()
        {
            return new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {sessionJwt}"
            };
        }

        /// <summary>
        /// Initialize private key import and get encryption bundle.
        /// This is the first step of the two-step private key import process.
        /// Aligned with initImportPrivateKey() in peak-sdk-browser.
        /// </summary>
        /// <returns>The import bundle, organization ID, and user ID needed to encrypt the private key.</returns>
        public async UniTask<InitImportPrivateKeyResult> InitImportPrivateKeyAsync()
        {
            // Get Turnkey credentials from JWT (aligned with peak-sdk-browser)
            var jwtPayload = SessionJwt.DecodeSessionJwt(sessionJwt);
            var organizationId = jwtPayload.TurnkeySubOrgId;
            var userId = jwtPayload.TurnkeyUserId;

            var turnkeyClient = CreateTurnkeyClient();

            var initBody = new TurnkeyUtils.InitImportPrivateKeyRequestBody
            {
                organizationId = organizationId,
                type = "ACTIVITY_TYPE_INIT_IMPORT_PRIVATE_KEY",
                timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                parameters = new TurnkeyUtils.InitImportPrivateKeyParameters
                {
                    userId = userId
                }
            };

            var signedInitImportRequest = turnkeyClient.StampInitImportPrivateKey(initBody);
            var initResponse = await httpClient.PostAsync<InitImportPrivateKeyResponse>(
                "public-api/v1/private-keys/init-import",
                new InitImportPrivateKeyRequest(signedInitImportRequest),
                CreateAuthHeaders());

            if (initResponse == null || string.IsNullOrEmpty(initResponse.importBundle))
            {
                throw new Exception("Turnkey init import response did not include a bundle");
            }

            return new InitImportPrivateKeyResult
            {
                importBundle = initResponse.importBundle,
                organizationId = organizationId,
                userId = userId
            };
        }

        /// <summary>
        /// Complete private key import using encrypted bundle.
        /// This is the second step of the two-step private key import process.
        /// Aligned with completeImportPrivateKey() in peak-sdk-browser.
        /// </summary>
        /// <param name="encryptedBundle">The encrypted private key bundle from TurnkeyUtils.EncryptPrivateKeyToBundle()</param>
        /// <param name="chainType">The chain type: "evm" or "solana"</param>
        /// <returns>The created account, account address, and account source information.</returns>
        public async UniTask<CompleteImportPrivateKeyResult> CompleteImportPrivateKeyAsync(string encryptedBundle, string chainType)
        {
            if (string.IsNullOrWhiteSpace(encryptedBundle))
            {
                throw new ArgumentException("Encrypted bundle must not be empty", nameof(encryptedBundle));
            }

            if (string.IsNullOrWhiteSpace(chainType))
            {
                throw new ArgumentException("Chain type must not be empty", nameof(chainType));
            }

            // Get Turnkey credentials from JWT (aligned with peak-sdk-browser)
            var jwtPayload = SessionJwt.DecodeSessionJwt(sessionJwt);
            var organizationId = jwtPayload.TurnkeySubOrgId;
            var userId = jwtPayload.TurnkeyUserId;

            // Determine curve and address format based on chainType (aligned with peak-sdk-browser)
            string curve;
            string addressFormat;

            switch (chainType.ToLowerInvariant())
            {
                case "evm":
                    curve = "CURVE_SECP256K1";
                    addressFormat = "ADDRESS_FORMAT_ETHEREUM";
                    break;
                case "solana":
                    curve = "CURVE_ED25519";
                    addressFormat = "ADDRESS_FORMAT_SOLANA";
                    break;
                default:
                    throw new ArgumentException($"Unsupported chain type: {chainType}. Supported: evm, solana", nameof(chainType));
            }

            var turnkeyClient = CreateTurnkeyClient();

            var completeBody = new TurnkeyUtils.ImportPrivateKeyRequestBody
            {
                organizationId = organizationId,
                type = "ACTIVITY_TYPE_IMPORT_PRIVATE_KEY",
                timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                parameters = new TurnkeyUtils.ImportPrivateKeyParameters
                {
                    userId = userId,
                    addressFormats = new[] { addressFormat },
                    curve = curve,
                    encryptedBundle = encryptedBundle,
                    privateKeyName = $"Imported Private Key {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"
                }
            };

            var signedCompleteRequest = turnkeyClient.StampImportPrivateKey(completeBody);
            var completeResponse = await httpClient.PostAsync<CompleteImportPrivateKeyResponse>(
                "public-api/v1/private-keys/complete-import",
                new CompleteImportPrivateKeyRequest(chainType, signedCompleteRequest),
                CreateAuthHeaders());

            if (completeResponse == null)
            {
                throw new Exception("Complete import response was null");
            }

            return new CompleteImportPrivateKeyResult
            {
                account = completeResponse.account,
                accountAddress = completeResponse.accountAddress,
                accountSource = completeResponse.accountSource
            };
        }

        /// <summary>
        /// Exports the encrypted private key bundle for a given address.
        /// The client must decrypt the bundle using TurnkeyUtils.DecryptExportBundle().
        /// Supports both private-key and recovery-phrase backed addresses.
        /// Aligned with exportPrivateKey() in peak-sdk-browser.
        /// </summary>
        /// <param name="address">The address to export the private key for</param>
        /// <param name="targetPublicKey">The P256 public key (uncompressed) to encrypt the export bundle with</param>
        /// <returns>The encrypted export bundle and organization ID for decryption</returns>
        public async UniTask<ExportPrivateKeyResult> ExportPrivateKeyAsync(string address, string targetPublicKey)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Address must not be empty", nameof(address));
            }

            if (string.IsNullOrWhiteSpace(targetPublicKey))
            {
                throw new ArgumentException("Target public key must not be empty", nameof(targetPublicKey));
            }

            // Get organization ID from JWT
            var jwtPayload = SessionJwt.DecodeSessionJwt(sessionJwt);
            var organizationId = jwtPayload.TurnkeySubOrgId;

            // Get account source info to determine export method
            var addressDetail = await GetAddressDetailAsync(address);
            if (addressDetail?.accountSource == null)
            {
                throw new Exception("Account source was not returned for the specified address");
            }

            var sourceType = addressDetail.accountSource.sourceType;
            var turnkeyClient = CreateTurnkeyClient();
            global::Turnkey.Http.SignedRequest signedExportRequest;

            // Create SignedRequest based on source type (aligned with peak-sdk-browser)
            if (string.Equals(sourceType, "private-key", StringComparison.OrdinalIgnoreCase))
            {
                var privateKeyId = addressDetail.accountSource.turnkeyResourceId;
                if (string.IsNullOrEmpty(privateKeyId))
                {
                    throw new Exception("turnkeyResourceId is missing from account source");
                }

                var exportBody = new TurnkeyUtils.ExportPrivateKeyRequestBody
                {
                    organizationId = organizationId,
                    type = "ACTIVITY_TYPE_EXPORT_PRIVATE_KEY",
                    timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                    parameters = new TurnkeyUtils.ExportPrivateKeyParameters
                    {
                        privateKeyId = privateKeyId,
                        targetPublicKey = targetPublicKey
                    }
                };
                signedExportRequest = turnkeyClient.StampExportPrivateKey(exportBody);
            }
            else if (string.Equals(sourceType, "recovery-phrase", StringComparison.OrdinalIgnoreCase))
            {
                var exportBody = new TurnkeyUtils.ExportWalletAccountRequestBody
                {
                    organizationId = organizationId,
                    type = "ACTIVITY_TYPE_EXPORT_WALLET_ACCOUNT",
                    timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                    parameters = new TurnkeyUtils.ExportWalletAccountParameters
                    {
                        address = address,
                        targetPublicKey = targetPublicKey
                    }
                };
                signedExportRequest = turnkeyClient.StampExportWalletAccount(exportBody);
            }
            else
            {
                throw new Exception($"Unsupported source type: '{sourceType}'");
            }

            var exportResponse = await httpClient.PostAsync<ExportPrivateKeyResponse>(
                "public-api/v1/private-keys/export",
                new ExportPrivateKeyRequest(sourceType, signedExportRequest),
                CreateAuthHeaders());

            if (exportResponse == null || string.IsNullOrEmpty(exportResponse.exportBundle))
            {
                throw new Exception("Export private key response did not contain a bundle");
            }

            return new ExportPrivateKeyResult
            {
                exportBundle = exportResponse.exportBundle,
                organizationId = organizationId
            };
        }

        private global::Turnkey.Http CreateTurnkeyClient()
        {
            return TurnkeyUtils.GetTurnkeyHttpClient(targetPrivateKey);
        }

        private async UniTask<GetAddressDetailResponse> GetAddressDetailAsync(string address)
        {
            var encodedAddress = Uri.EscapeDataString(address);
            var endpoint = $"public-api/v1/accounts/get-address-detail?address={encodedAddress}";
            return await httpClient.GetAsync<GetAddressDetailResponse>(endpoint, CreateAuthHeaders());
        }

    }
}
