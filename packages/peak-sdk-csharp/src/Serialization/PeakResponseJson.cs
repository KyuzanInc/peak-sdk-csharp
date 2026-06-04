using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace KyuzanInc.Peak.Sdk
{
    /// <summary>
    /// Deserializes HTTP response bodies via System.Text.Json source generation
    /// (<see cref="PeakJsonContext"/>). AOT- / IL2CPP-safe: a type not registered
    /// in the context throws rather than falling back to reflection.
    /// </summary>
    internal static class PeakResponseJson
    {
        internal static T? Deserialize<T>(string body) where T : class
        {
            var typeInfo = (JsonTypeInfo<T>?)PeakJsonContext.Default.GetTypeInfo(typeof(T))
                ?? throw new PeakError(PeakErrorCode.InvalidArgument,
                    $"Type {typeof(T).Name} is not registered in PeakJsonContext.");
            return JsonSerializer.Deserialize(body, typeInfo);
        }
    }
}
