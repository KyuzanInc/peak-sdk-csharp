using System.Text.Json.Serialization;
using KyuzanInc.Peak.Sdk.Models;
using KyuzanInc.Peak.Sdk.Utils;

namespace KyuzanInc.Peak.Sdk
{
    /// <summary>
    /// Source-generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>
    /// covering every type the SDK serialises or deserialises. AOT- / IL2CPP-safe.
    ///
    /// Naming policy is CamelCase: C# PascalCase property names round-trip to
    /// lowerCamelCase JSON keys to match peak-server's OpenAPI spec and the
    /// Unity port's <c>JsonUtility</c> output byte-for-byte.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = false,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    // Entities
    [JsonSerializable(typeof(AccountResponse))]
    [JsonSerializable(typeof(AccountResponse[]))]
    [JsonSerializable(typeof(AccountAddressResponse))]
    [JsonSerializable(typeof(AccountAddressResponse[]))]
    [JsonSerializable(typeof(UserResponse))]
    [JsonSerializable(typeof(AccountSourceResponse))]
    // Response wrappers
    [JsonSerializable(typeof(ListAccountsResponse))]
    [JsonSerializable(typeof(ListAccountAddressesResponse))]
    [JsonSerializable(typeof(GetAddressDetailResponse))]
    [JsonSerializable(typeof(InitOtpLoginResponse))]
    [JsonSerializable(typeof(CompleteOtpLoginResponse))]
    [JsonSerializable(typeof(InitImportPrivateKeyResponse))]
    [JsonSerializable(typeof(CompleteImportPrivateKeyResponse))]
    [JsonSerializable(typeof(ExportPrivateKeyResponse))]
    // Requests
    [JsonSerializable(typeof(InitOtpLoginRequest))]
    [JsonSerializable(typeof(CompleteOtpLoginRequest))]
    [JsonSerializable(typeof(UpdateAccountDisplayNameRequest))]
    [JsonSerializable(typeof(InitImportPrivateKeyRequest))]
    [JsonSerializable(typeof(CompleteImportPrivateKeyRequest))]
    [JsonSerializable(typeof(ExportPrivateKeyRequest))]
    // SDK-level results (rarely serialised across the wire but registered for completeness)
    [JsonSerializable(typeof(KeyPair))]
    [JsonSerializable(typeof(CompleteOtpLoginResult))]
    [JsonSerializable(typeof(InitImportPrivateKeyResult))]
    [JsonSerializable(typeof(CompleteImportPrivateKeyResult))]
    [JsonSerializable(typeof(ExportPrivateKeyResult))]
    [JsonSerializable(typeof(AuthenticatedData))]
    [JsonSerializable(typeof(SessionJwtPayload))]
    [JsonSerializable(typeof(SessionJwtPayloadJson))]
    // Storage payload
    [JsonSerializable(typeof(KyuzanInc.Peak.Sdk.Storage.SessionData))]
    // Codegen-style escape valves: object root for SDK responses that vary by endpoint
    [JsonSerializable(typeof(System.Collections.Generic.Dictionary<string, object?>))]
    internal partial class PeakJsonContext : JsonSerializerContext
    {
    }
}
