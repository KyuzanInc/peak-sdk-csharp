using System;
using System.Linq;
using KyuzanInc.Peak.Sdk.Models;
using Gen = KyuzanInc.Peak.PublicApiClient.Model;

namespace KyuzanInc.Peak.Sdk.Mapping
{
    /// <summary>
    /// Maps internal generated response DTOs to the public System.Text.Json DTOs.
    /// Generated enums become their wire strings, decimal indexes become int, and
    /// List&lt;T&gt; becomes T[]. Public DTO fields with no spec source (e.g.
    /// UserResponse.IsAuthenticated — not in the spec) are left at default.
    /// </summary>
    internal static class GeneratedDtoMappers
    {
        // Convert a generated enum to its wire string via the [EnumMember] value
        // that the generated [JsonConverter(StringEnumConverter)] uses. Undefined
        // values (e.g. an absent, default(0) enum) map to null, matching the old
        // "field absent -> null string" behavior.
        private static string? ToWire<TEnum>(TEnum value) where TEnum : struct, Enum =>
            Enum.IsDefined(typeof(TEnum), value)
                ? Newtonsoft.Json.JsonConvert.SerializeObject(value).Trim('"')
                : null;

        // The spec types accountIndex as `number` (the generated DTO is decimal),
        // but the public DTO is int. Reject non-integral or out-of-range values as
        // PeakErrorCode.InvalidResponse rather than silently truncating or letting
        // an OverflowException escape — matching how the old STJ-into-int path
        // rejected bad numbers.
        private static int ToAccountIndex(decimal value)
        {
            if (decimal.Truncate(value) != value || value < int.MinValue || value > int.MaxValue)
            {
                throw new PeakError(PeakErrorCode.InvalidResponse,
                    $"accountIndex '{value}' is not a valid 32-bit integer");
            }
            return (int)value;
        }

        // --- entities ---

        internal static UserResponse ToPublic(this Gen.UserResponseDto d) => new()
        {
            Id = d.Id,
            Email = d.Email,
            OriginProjectId = d.OriginProjectId,
            TurnkeySubOrgId = d.TurnkeySubOrgId,
            TurnkeyRootUserId = d.TurnkeyRootUserId,
            DeletionStatus = ToWire(d.DeletionStatus),
            // IsAuthenticated has no spec field; left default(false).
        };

        internal static AccountResponse ToPublic(this Gen.AccountResponseDto d) => new()
        {
            Id = d.Id,
            UserId = d.UserId,
            AccountSourceId = d.AccountSourceId,
            AccountIndex = ToAccountIndex(d.AccountIndex),
            OriginProjectId = d.OriginProjectId,
            DisplayName = d.DisplayName,
        };

        internal static AccountAddressResponse ToPublic(this Gen.AccountAddressResponseDto d) => new()
        {
            Id = d.Id,
            AccountId = d.AccountId,
            Address = d.Address,
            ChainType = ToWire(d.ChainType),
            BitcoinAddressType = d.BitcoinAddressType is { } bt ? ToWire(bt) : null,
        };

        internal static AccountSourceResponse ToPublic(this Gen.AccountSourceResponseDto d) => new()
        {
            Id = d.Id,
            UserId = d.UserId,
            OriginProjectId = d.OriginProjectId,
            SourceType = ToWire(d.SourceType),
            CreationMethod = ToWire(d.CreationMethod),
            TurnkeyResourceId = d.TurnkeyResourceId,
            DisplayName = d.DisplayName,
        };

        // --- responses ---

        internal static InitOtpLoginResponse ToPublic(this Gen.InitOtpLoginResponseDto d) => new()
        {
            OtpId = d.OtpId,
        };

        internal static CompleteOtpLoginResponse ToPublic(this Gen.CompleteOtpLoginResponseDto d) => new()
        {
            User = d.User?.ToPublic(),
            SessionJwt = d.SessionJwt,
            IsNewUser = d.IsNewUser,
            AccountSource = d.AccountSource?.ToPublic(),
            Account = d.Account?.ToPublic(),
            AccountAddresses = d.AccountAddresses?.Select(a => a.ToPublic()).ToArray(),
        };

        internal static ListAccountsResponse ToPublic(this Gen.ListAccountsResponseDto d) => new()
        {
            Accounts = d.Accounts?.Select(a => a.ToPublic()).ToArray(),
        };

        internal static ListAccountAddressesResponse ToPublic(this Gen.ListAccountAddressesResponseDto d) => new()
        {
            AccountAddresses = d.AccountAddresses?.Select(a => a.ToPublic()).ToArray(),
        };

        internal static GetAddressDetailResponse ToPublic(this Gen.GetAccountAddressWithAccountAndSourceResponseDto d) => new()
        {
            AccountAddress = d.AccountAddress?.ToPublic(),
            Account = d.Account?.ToPublic(),
            AccountSource = d.AccountSource?.ToPublic(),
        };

        internal static InitImportPrivateKeyResponse ToPublic(this Gen.InitImportPrivateKeyResponseDto d) => new()
        {
            ImportBundle = d.ImportBundle,
        };

        internal static CompleteImportPrivateKeyResponse ToPublic(this Gen.CompleteImportPrivateKeyResponseDto d) => new()
        {
            Account = d.Account?.ToPublic(),
            AccountAddress = d.AccountAddress?.ToPublic(),
            AccountSource = d.AccountSource?.ToPublic(),
        };

        internal static ExportPrivateKeyResponse ToPublic(this Gen.ExportPrivateKeyResponseDto d) => new()
        {
            ExportBundle = d.ExportBundle,
        };
    }
}
