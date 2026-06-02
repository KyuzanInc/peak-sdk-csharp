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
    /// <see cref="TolerantStringEnumConverter"/>, which maps an unknown string to
    /// the enum default (or <c>null</c> for a nullable enum). The mappers in
    /// <c>GeneratedDtoMappers</c> then render that default as <c>null</c> on the
    /// public <c>string?</c> field — restoring the pre-generated-client passthrough
    /// where an unknown value flowed through instead of failing the whole call.
    /// Non-enum members keep their generated converters, so required-field and
    /// numeric validation still fail closed as before.
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
    /// <see cref="StringEnumConverter"/> that returns the enum default (or
    /// <c>null</c> for a nullable enum) instead of throwing when the wire string
    /// is not a known enum member. Numbers and well-formed strings keep the base
    /// behavior.
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
            try
            {
                return base.ReadJson(reader, objectType, existingValue, serializer);
            }
            catch (JsonException)
            {
                // Unknown enum string. A nullable enum tolerates null; a
                // non-nullable enum falls back to its default so the rest of the
                // payload still deserializes. Either way the mappers turn this
                // into a null public value (the old passthrough semantics). This
                // converter is only attached to enum / Nullable<enum> members
                // (see TolerantEnumContractResolver), so the underlying type is
                // always a concrete enum.
                var underlying = Nullable.GetUnderlyingType(objectType);
                if (underlying != null)
                {
                    return null;
                }

                return Activator.CreateInstance(objectType);
            }
        }
    }
}
