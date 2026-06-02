using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace KyuzanInc.Peak.Sdk
{
    /// <summary>
    /// Newtonsoft contract resolver that makes enum members on the generated
    /// response DTOs tolerant of values the client does not know yet.
    /// </summary>
    /// <remarks>
    /// The generated DTOs decorate each enum with a bare
    /// <c>[JsonConverter(typeof(StringEnumConverter))]</c>, which hard-fails
    /// deserialization on any unrecognized string. The peak-server spec is synced
    /// from an independently-evolving monorepo (see CLAUDE.md), so an additive
    /// server change — a new <c>chainType</c>, <c>sourceType</c>,
    /// <c>creationMethod</c>, <c>deletionStatus</c>, or <c>bitcoinAddressType</c> —
    /// would otherwise reject an otherwise-valid response as
    /// <see cref="PeakErrorCode.InvalidResponse"/>.
    ///
    /// A member-level <c>[JsonConverter]</c> attribute wins over any converter in
    /// <see cref="JsonSerializerSettings.Converters"/>, so the only reliable way
    /// to override it is to replace the property's converter directly via the
    /// contract resolver. For enum members this resolver swaps in
    /// <see cref="TolerantStringEnumConverter"/>, which maps any unrecognized wire
    /// value (an unknown string, or a non-string token) to the enum default (or
    /// <c>null</c> for a nullable enum). The mappers in <c>GeneratedDtoMappers</c>
    /// then render that as <c>null</c> on the public <c>string?</c> field.
    ///
    /// This is a deliberate trade-off of adopting the generated enum DTOs: an
    /// unknown value surfaces as <c>null</c> ("not recognized") rather than
    /// failing the whole response (the generated converter's behavior) — but it is
    /// not preserved as the raw wire string (the pre-generated-client behavior),
    /// because a C# enum cannot hold an out-of-set value. Adopting a new server
    /// enum value therefore requires resyncing the spec and regenerating.
    ///
    /// Non-enum members keep their generated converters, so required-field and
    /// numeric validation (e.g. <c>accountIndex</c>) still fail closed as before.
    /// </remarks>
    internal sealed class TolerantEnumContractResolver : DefaultContractResolver
    {
        internal static readonly TolerantEnumContractResolver Instance = new();

        protected override JsonProperty CreateProperty(
            System.Reflection.MemberInfo member,
            MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            var propertyType = property.PropertyType;
            if (propertyType != null)
            {
                var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
                if (underlying.IsEnum)
                {
                    property.Converter = TolerantStringEnumConverter.Instance;
                }
            }

            return property;
        }
    }

    /// <summary>
    /// <see cref="StringEnumConverter"/> that maps any wire value the client does
    /// not recognize as a known enum member to the enum default (or <c>null</c>
    /// for a nullable enum) instead of throwing or coercing. The peak-server wire
    /// contract is string enums, so only a known string token maps to a member;
    /// an unknown string, a number, or any other token is treated as
    /// unrecognized. In particular a numeric token is NOT coerced to the member
    /// with that ordinal (which would turn <c>1</c> into a valid-looking but wrong
    /// value), and the <see cref="GeneratedDtoMappers"/> render every unrecognized
    /// value as <c>null</c> on the public <c>string?</c> field.
    /// </summary>
    internal sealed class TolerantStringEnumConverter : StringEnumConverter
    {
        internal static readonly TolerantStringEnumConverter Instance = new();

        public override object? ReadJson(
            JsonReader reader,
            Type objectType,
            object? existingValue,
            JsonSerializer serializer)
        {
            // This converter is only attached to enum / Nullable<enum> members
            // (see TolerantEnumContractResolver), so objectType is always a
            // concrete enum or Nullable<enum>.
            var underlying = Nullable.GetUnderlyingType(objectType);

            if (reader.TokenType == JsonToken.String)
            {
                try
                {
                    return base.ReadJson(reader, objectType, existingValue, serializer);
                }
                catch (JsonException)
                {
                    // Unknown enum string — fall through to the unrecognized value.
                }
            }
            else
            {
                // A non-string token (a number the base converter would otherwise
                // coerce to a wrong member by ordinal, a bool, an object/array,
                // etc.). Consume it so the reader advances, then treat it as
                // unrecognized rather than silently mapping it to a known member.
                reader.Skip();
            }

            // Unrecognized: null for a nullable enum, the enum default otherwise.
            // The mappers turn either into a null public value, so an unknown wire
            // value never becomes a wrong known value, and the rest of the payload
            // still deserializes.
            return underlying != null ? null : Activator.CreateInstance(objectType);
        }
    }
}
