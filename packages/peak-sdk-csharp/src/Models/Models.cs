// Ported from upstream-snapshots/peak-sdk-unity/Runtime/Models/*.
// All `[Serializable]` (Unity attribute) removed; properties auto-generated for
// System.Text.Json source-gen access. Field naming policy is CamelCase via the
// PeakJsonContext so wire JSON is byte-equivalent (lowerCamelCase) with the
// peak-server OpenAPI spec and the Unity output.

namespace KyuzanInc.Peak.Sdk.Models
{
    // --- Entities (mirror peak-server response DTOs) ---

    public sealed class AccountResponse
    {
        public string? Id { get; set; }
        public string? UserId { get; set; }
        public string? AccountSourceId { get; set; }
        public int AccountIndex { get; set; }
        public string? OriginProjectId { get; set; }
        public string? DisplayName { get; set; }
    }

    public sealed class AccountAddressResponse
    {
        public string? Id { get; set; }
        public string? AccountId { get; set; }
        public string? Address { get; set; }
        public string? ChainType { get; set; }
        public string? BitcoinAddressType { get; set; }
    }

    public sealed class UserResponse
    {
        public string? Id { get; set; }
        public string? Email { get; set; }
        public string? OriginProjectId { get; set; }
        public string? TurnkeySubOrgId { get; set; }
        public string? TurnkeyRootUserId { get; set; }
        public string? DeletionStatus { get; set; }
        public bool IsAuthenticated { get; set; }
    }

    public sealed class AccountSourceResponse
    {
        public string? Id { get; set; }
        public string? UserId { get; set; }
        public string? OriginProjectId { get; set; }
        public string? SourceType { get; set; }
        public string? CreationMethod { get; set; }
        public string? TurnkeyResourceId { get; set; }
        public string? DisplayName { get; set; }
    }

    // --- Response wrappers ---

    public sealed class ListAccountsResponse
    {
        public AccountResponse[]? Accounts { get; set; }
    }

    public sealed class ListAccountAddressesResponse
    {
        public AccountAddressResponse[]? AccountAddresses { get; set; }
    }

    public sealed class GetAddressDetailResponse
    {
        public AccountAddressResponse? AccountAddress { get; set; }
        public AccountResponse? Account { get; set; }
        public AccountSourceResponse? AccountSource { get; set; }
    }

    public sealed class InitOtpLoginResponse
    {
        public string? OtpId { get; set; }
    }

    public sealed class CompleteOtpLoginResponse
    {
        public UserResponse? User { get; set; }
        public string? SessionJwt { get; set; }
        public bool IsNewUser { get; set; }
        public AccountSourceResponse? AccountSource { get; set; }
        public AccountResponse? Account { get; set; }
        public AccountAddressResponse[]? AccountAddresses { get; set; }
    }

    public sealed class InitImportPrivateKeyResponse
    {
        public string? ImportBundle { get; set; }
    }

    public sealed class CompleteImportPrivateKeyResponse
    {
        public AccountResponse? Account { get; set; }
        public AccountAddressResponse? AccountAddress { get; set; }
        public AccountSourceResponse? AccountSource { get; set; }
    }

    public sealed class ExportPrivateKeyResponse
    {
        public string? ExportBundle { get; set; }
    }

    // --- Request DTOs ---

    public sealed class InitOtpLoginRequest
    {
        public string? Email { get; set; }
    }

    public sealed class CompleteOtpLoginRequest
    {
        public string? Email { get; set; }
        public string? OtpId { get; set; }
        public string? OtpCode { get; set; }
        public string? TargetPublicKey { get; set; }
        public bool Signup { get; set; }
    }

    public sealed class UpdateAccountDisplayNameRequest
    {
        public string? AccountId { get; set; }
        public string? DisplayName { get; set; }
    }

    // These three request envelopes wrap a global::Turnkey.Http.SignedRequest.
    // They are internal so that Turnkey type does not leak onto the SDK's
    // public surface (so no consumer is forced to reference Turnkey.*). They are
    // only built by PrivateKeyService and serialized via PeakJsonContext;
    // source-gen handles internal [JsonSerializable] types fine. Tests reach
    // them through InternalsVisibleTo.
    internal sealed class InitImportPrivateKeyRequest
    {
        public global::Turnkey.Http.SignedRequest? SignedInitImportPrivateKeyRequest { get; set; }
    }

    internal sealed class CompleteImportPrivateKeyRequest
    {
        public string? ChainType { get; set; }
        public global::Turnkey.Http.SignedRequest? SignedCompleteImportPrivateKeyRequest { get; set; }
    }

    internal sealed class ExportPrivateKeyRequest
    {
        public string? SourceType { get; set; }
        public global::Turnkey.Http.SignedRequest? SignedExportPrivateKeyRequest { get; set; }
    }

    // --- SDK-level result types ---

    public sealed class KeyPair
    {
        public string? PublicKey { get; set; }
        public string? PrivateKey { get; set; }
    }

    public sealed class CompleteOtpLoginResult
    {
        public UserResponse? User { get; set; }
        public string? SessionJwt { get; set; }
        public bool IsNewUser { get; set; }
        public AccountSourceResponse? AccountSource { get; set; }
        public AccountResponse? Account { get; set; }
        public AccountAddressResponse[]? AccountAddresses { get; set; }
        public KeyPair? KeyPair { get; set; }
    }

    public sealed class InitImportPrivateKeyResult
    {
        public string? ImportBundle { get; set; }
        public string? OrganizationId { get; set; }
        public string? UserId { get; set; }
    }

    public sealed class CompleteImportPrivateKeyResult
    {
        public AccountResponse? Account { get; set; }
        public AccountAddressResponse? AccountAddress { get; set; }
        public AccountSourceResponse? AccountSource { get; set; }
    }

    public sealed class ExportPrivateKeyResult
    {
        public string? ExportBundle { get; set; }
        public string? OrganizationId { get; set; }
    }

    public sealed class AuthenticatedData
    {
        public string? Email { get; set; }
        public string? SessionJwt { get; set; }
        public Utils.SessionJwtPayload? SessionJwtPayload { get; set; }
    }
}
