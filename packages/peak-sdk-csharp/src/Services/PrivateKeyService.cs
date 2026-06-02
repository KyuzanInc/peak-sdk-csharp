// Ported from upstream-snapshots/peak-sdk-unity/Runtime/Services/Authenticated/PrivateKeyService.cs.
// Logic 1:1 with the Unity port; UniTask -> Task and the TurnkeyUtils.* facade
// is inlined as direct global::Turnkey.* calls.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KyuzanInc.Peak.Sdk.Models;
using KyuzanInc.Peak.Sdk.Utils;
using KyuzanInc.Peak.Sdk.Mapping;
using Gen = KyuzanInc.Peak.PublicApiClient.Model;

namespace KyuzanInc.Peak.Sdk.Services
{
    public sealed class PrivateKeyService
    {
        private readonly IPeakHttpClient httpClient;
        private readonly string sessionJwt;
        private readonly string targetPrivateKey;
        private readonly AccountService accountService;

        public PrivateKeyService(
            string apiUrl,
            string projectApiKey,
            string sessionJwt,
            string targetPrivateKey,
            IPeakHttpClient? httpClient = null,
            AccountService? accountService = null)
        {
            if (string.IsNullOrWhiteSpace(sessionJwt))
            {
                throw new PeakError(PeakErrorCode.InvalidArgument, "Session JWT is required");
            }
            if (string.IsNullOrWhiteSpace(targetPrivateKey))
            {
                throw new PeakError(PeakErrorCode.InvalidArgument, "Target private key is required");
            }

            this.httpClient = httpClient ?? new DefaultPeakHttpClient(apiUrl, projectApiKey);
            this.sessionJwt = sessionJwt;
            this.targetPrivateKey = targetPrivateKey;
            this.accountService = accountService ?? new AccountService(apiUrl, projectApiKey, sessionJwt, this.httpClient);
        }

        private Dictionary<string, string> CreateAuthHeaders() =>
            new() { ["Authorization"] = $"Bearer {sessionJwt}" };

        private global::Turnkey.Http CreateTurnkeyClient() =>
            global::Turnkey.Http.FromTargetPrivateKey(targetPrivateKey);

        public async Task<InitImportPrivateKeyResult> InitImportPrivateKeyAsync(CancellationToken cancellationToken = default)
        {
            var jwtPayload = SessionJwt.DecodeSessionJwt(sessionJwt);
            var organizationId = jwtPayload.TurnkeySubOrgId ?? throw new PeakError(PeakErrorCode.InvalidJwt, "JWT missing organizationId");
            var userId = jwtPayload.TurnkeyUserId ?? throw new PeakError(PeakErrorCode.InvalidJwt, "JWT missing userId");

            var turnkeyClient = CreateTurnkeyClient();
            var initBody = new global::Turnkey.Http.InitImportPrivateKeyRequestBody
            {
                OrganizationId = organizationId,
                Type = "ACTIVITY_TYPE_INIT_IMPORT_PRIVATE_KEY",
                TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                Parameters = new global::Turnkey.Http.InitImportPrivateKeyParameters { UserId = userId },
            };
            var signedInitImportRequest = turnkeyClient.StampInitImportPrivateKey(initBody);

            var initResponse = await httpClient.PostAsync<InitImportPrivateKeyRequest, Gen.InitImportPrivateKeyResponseDto>(
                "public-api/v1/private-keys/init-import",
                new InitImportPrivateKeyRequest { SignedInitImportPrivateKeyRequest = signedInitImportRequest },
                CreateAuthHeaders(),
                cancellationToken).ConfigureAwait(false);

            if (initResponse == null || string.IsNullOrEmpty(initResponse.ImportBundle))
            {
                throw new PeakError(PeakErrorCode.TurnkeyError, "Turnkey init import response did not include a bundle");
            }

            return new InitImportPrivateKeyResult
            {
                ImportBundle = initResponse.ImportBundle,
                OrganizationId = organizationId,
                UserId = userId,
            };
        }

        public async Task<CompleteImportPrivateKeyResult> CompleteImportPrivateKeyAsync(string encryptedBundle, string chainType, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(encryptedBundle))
                throw new PeakError(PeakErrorCode.InvalidArgument, "Encrypted bundle must not be empty");
            if (string.IsNullOrWhiteSpace(chainType))
                throw new PeakError(PeakErrorCode.InvalidArgument, "Chain type must not be empty");

            var jwtPayload = SessionJwt.DecodeSessionJwt(sessionJwt);
            var organizationId = jwtPayload.TurnkeySubOrgId ?? throw new PeakError(PeakErrorCode.InvalidJwt, "JWT missing organizationId");
            var userId = jwtPayload.TurnkeyUserId ?? throw new PeakError(PeakErrorCode.InvalidJwt, "JWT missing userId");

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
                    throw new PeakError(PeakErrorCode.InvalidArgument,
                        $"Unsupported chain type: {chainType}. Supported: evm, solana");
            }

            var turnkeyClient = CreateTurnkeyClient();
            var completeBody = new global::Turnkey.Http.ImportPrivateKeyRequestBody
            {
                OrganizationId = organizationId,
                Type = "ACTIVITY_TYPE_IMPORT_PRIVATE_KEY",
                TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                Parameters = new global::Turnkey.Http.ImportPrivateKeyParameters
                {
                    UserId = userId,
                    AddressFormats = new[] { addressFormat },
                    Curve = curve,
                    EncryptedBundle = encryptedBundle,
                    PrivateKeyName = $"Imported Private Key {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                },
            };
            var signedCompleteRequest = turnkeyClient.StampImportPrivateKey(completeBody);

            var completeResponse = await httpClient.PostAsync<CompleteImportPrivateKeyRequest, Gen.CompleteImportPrivateKeyResponseDto>(
                "public-api/v1/private-keys/complete-import",
                new CompleteImportPrivateKeyRequest { ChainType = chainType, SignedCompleteImportPrivateKeyRequest = signedCompleteRequest },
                CreateAuthHeaders(),
                cancellationToken).ConfigureAwait(false);

            if (completeResponse == null)
            {
                throw new PeakError(PeakErrorCode.TurnkeyError, "Complete import response was null");
            }

            return new CompleteImportPrivateKeyResult
            {
                Account = completeResponse.Account?.ToPublic(),
                AccountAddress = completeResponse.AccountAddress?.ToPublic(),
                AccountSource = completeResponse.AccountSource?.ToPublic(),
            };
        }

        public async Task<ExportPrivateKeyResult> ExportPrivateKeyAsync(string address, string targetPublicKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new PeakError(PeakErrorCode.InvalidArgument, "Address must not be empty");
            if (string.IsNullOrWhiteSpace(targetPublicKey))
                throw new PeakError(PeakErrorCode.InvalidArgument, "Target public key must not be empty");

            var jwtPayload = SessionJwt.DecodeSessionJwt(sessionJwt);
            var organizationId = jwtPayload.TurnkeySubOrgId ?? throw new PeakError(PeakErrorCode.InvalidJwt, "JWT missing organizationId");

            var addressDetail = await accountService.GetAddressDetailAsync(address, cancellationToken).ConfigureAwait(false);
            if (addressDetail?.AccountSource == null)
            {
                throw new PeakError(PeakErrorCode.InvalidResponse, "Account source was not returned for the specified address");
            }

            var sourceType = addressDetail.AccountSource.SourceType;
            var turnkeyClient = CreateTurnkeyClient();
            global::Turnkey.Http.SignedRequest signedExportRequest;

            if (string.Equals(sourceType, "private-key", StringComparison.OrdinalIgnoreCase))
            {
                var privateKeyId = addressDetail.AccountSource.TurnkeyResourceId
                    ?? throw new PeakError(PeakErrorCode.InvalidResponse, "turnkeyResourceId is missing from account source");

                var exportBody = new global::Turnkey.Http.ExportPrivateKeyRequestBody
                {
                    OrganizationId = organizationId,
                    Type = "ACTIVITY_TYPE_EXPORT_PRIVATE_KEY",
                    TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                    Parameters = new global::Turnkey.Http.ExportPrivateKeyParameters
                    {
                        PrivateKeyId = privateKeyId,
                        TargetPublicKey = targetPublicKey,
                    },
                };
                signedExportRequest = turnkeyClient.StampExportPrivateKey(exportBody);
            }
            else if (string.Equals(sourceType, "recovery-phrase", StringComparison.OrdinalIgnoreCase))
            {
                var exportBody = new global::Turnkey.Http.ExportWalletAccountRequestBody
                {
                    OrganizationId = organizationId,
                    Type = "ACTIVITY_TYPE_EXPORT_WALLET_ACCOUNT",
                    TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                    Parameters = new global::Turnkey.Http.ExportWalletAccountParameters
                    {
                        Address = address,
                        TargetPublicKey = targetPublicKey,
                    },
                };
                signedExportRequest = turnkeyClient.StampExportWalletAccount(exportBody);
            }
            else
            {
                throw new PeakError(PeakErrorCode.InvalidResponse, $"Unsupported source type: '{sourceType}'");
            }

            var exportResponse = await httpClient.PostAsync<ExportPrivateKeyRequest, Gen.ExportPrivateKeyResponseDto>(
                "public-api/v1/private-keys/export",
                new ExportPrivateKeyRequest { SourceType = sourceType, SignedExportPrivateKeyRequest = signedExportRequest },
                CreateAuthHeaders(),
                cancellationToken).ConfigureAwait(false);

            if (exportResponse == null || string.IsNullOrEmpty(exportResponse.ExportBundle))
            {
                throw new PeakError(PeakErrorCode.TurnkeyError, "Export private key response did not contain a bundle");
            }

            return new ExportPrivateKeyResult
            {
                ExportBundle = exportResponse.ExportBundle,
                OrganizationId = organizationId,
            };
        }
    }
}
