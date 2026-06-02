using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace KyuzanInc.Peak.Sdk
{
    /// <summary>
    /// Deserializes HTTP response bodies. Types from the internal generated
    /// client assembly are Newtonsoft-shaped (Section 3 of the spec), so they go
    /// through Newtonsoft; the SDK's own types keep using STJ source generation.
    /// </summary>
    internal static class PeakResponseJson
    {
        private static readonly Assembly GeneratedClientAssembly =
            typeof(KyuzanInc.Peak.PublicApiClient.Model.InitOtpLoginResponseDto).Assembly;

        // Tolerate enum values the client does not know yet (forward compat): an
        // additive server enum (e.g. a future chainType) maps to the enum default
        // instead of hard-failing the whole response. See
        // TolerantEnumContractResolver.
        private static readonly Newtonsoft.Json.JsonSerializerSettings GeneratedClientSettings =
            new() { ContractResolver = TolerantEnumContractResolver.Instance };

        internal static T? Deserialize<T>(string body) where T : class
        {
            if (typeof(T).Assembly == GeneratedClientAssembly)
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(body, GeneratedClientSettings);
            }

            var typeInfo = (JsonTypeInfo<T>?)PeakJsonContext.Default.GetTypeInfo(typeof(T))
                ?? throw new PeakError(PeakErrorCode.InvalidArgument,
                    $"Type {typeof(T).Name} is not registered in PeakJsonContext.");
            return JsonSerializer.Deserialize(body, typeInfo);
        }
    }
}
