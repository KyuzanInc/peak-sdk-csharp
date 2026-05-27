// Source-generated System.Text.Json context for AOT / IL2CPP safety.
// Per docs/security/crypto-port-policy.md D6, all JsonSerializer.Serialize / Deserialize
// calls in this assembly MUST go through this context so that reflection-based
// serialization paths are never reached. That guarantees the package can run under
// IL2CPP and other AOT-strip toolchains without surprise.

using System.Text.Json.Serialization;

namespace KyuzanInc.Turnkey.Sdk
{
    /// <summary>
    /// Source-generated JSON context covering every type this assembly serialises or
    /// deserialises. Added to ensure IL2CPP / AOT / trimming safety.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = false,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    // ApiKeyStamper inner DTO
    [JsonSerializable(typeof(ApiKeyStamper.TurnkeyStamp))]
    // Http inner DTOs (kept nested per D16 to mirror the Unity port surface)
    [JsonSerializable(typeof(Http.SignedRequest))]
    [JsonSerializable(typeof(Http.Stamp))]
    [JsonSerializable(typeof(Http.WhoamiRequestBody))]
    [JsonSerializable(typeof(Http.InitImportPrivateKeyRequestBody))]
    [JsonSerializable(typeof(Http.InitImportPrivateKeyParameters))]
    [JsonSerializable(typeof(Http.ImportPrivateKeyRequestBody))]
    [JsonSerializable(typeof(Http.ImportPrivateKeyParameters))]
    [JsonSerializable(typeof(Http.ExportPrivateKeyRequestBody))]
    [JsonSerializable(typeof(Http.ExportPrivateKeyParameters))]
    [JsonSerializable(typeof(Http.ExportWalletAccountRequestBody))]
    [JsonSerializable(typeof(Http.ExportWalletAccountParameters))]
    // Crypto.FormatHpkeBuf output shape
    [JsonSerializable(typeof(Crypto.HpkeBufferOutput))]
    internal partial class TurnkeyJsonContext : JsonSerializerContext
    {
    }
}
