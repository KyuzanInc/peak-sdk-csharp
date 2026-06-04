using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Serialization.Metadata;
using FluentAssertions;
using KyuzanInc.Peak.Sdk;
using KyuzanInc.Peak.Sdk.Models;
using Xunit;
using Gen = KyuzanInc.Peak.PublicApiClient.Model;

namespace KyuzanInc.Peak.Sdk.Tests
{
    // P0 contract test (spec §6.7), the only guard for generated-client -> hand-DTO
    // drift now that #14's compile-time binding is gone. For each adopted endpoint:
    //   1. coverage: every generated wire field is modelled by the public DTO,
    //   2. no silent public-only additions beyond an explicit allowlist,
    //   3. shared fields share a JSON type-category.
    // It does NOT assert required-ness (spec §6.3 intentionally relaxes that).
    [Trait("Category", "Contract")]
    public class GeneratedDtoContractTests
    {
        // (public DTO, generated DTO) pairs. The internal update-display-name
        // envelope pairs with the generated UpdateAccountDisplayNameResponseDto.
        private static readonly (Type Public, Type Generated)[] Pairs =
        {
            (typeof(UserResponse), typeof(Gen.UserResponseDto)),
            (typeof(AccountResponse), typeof(Gen.AccountResponseDto)),
            (typeof(AccountAddressResponse), typeof(Gen.AccountAddressResponseDto)),
            (typeof(AccountSourceResponse), typeof(Gen.AccountSourceResponseDto)),
            (typeof(InitOtpLoginResponse), typeof(Gen.InitOtpLoginResponseDto)),
            (typeof(CompleteOtpLoginResponse), typeof(Gen.CompleteOtpLoginResponseDto)),
            (typeof(ListAccountsResponse), typeof(Gen.ListAccountsResponseDto)),
            (typeof(ListAccountAddressesResponse), typeof(Gen.ListAccountAddressesResponseDto)),
            (typeof(GetAddressDetailResponse), typeof(Gen.GetAccountAddressWithAccountAndSourceResponseDto)),
            (typeof(InitImportPrivateKeyResponse), typeof(Gen.InitImportPrivateKeyResponseDto)),
            (typeof(CompleteImportPrivateKeyResponse), typeof(Gen.CompleteImportPrivateKeyResponseDto)),
            (typeof(ExportPrivateKeyResponse), typeof(Gen.ExportPrivateKeyResponseDto)),
            (typeof(UpdateAccountDisplayNameEnvelope), typeof(Gen.UpdateAccountDisplayNameResponseDto)),
        };

        // Legacy public-only fields with no server-spec source (spec §6.7). Adding
        // an entry here must be a deliberate decision, surfaced in review.
        private static readonly Dictionary<Type, HashSet<string>> PublicOnlyAllowlist = new()
        {
            [typeof(UserResponse)] = new HashSet<string>(StringComparer.Ordinal) { "isAuthenticated" },
        };

        [Fact]
        public void PublicDtos_CoverEveryGeneratedWireField()
        {
            var failures = new List<string>();

            foreach (var (pub, gen) in Pairs)
            {
                var pubFields = PublicWireFields(pub);
                var genFields = GeneratedWireFields(gen);
                var allow = PublicOnlyAllowlist.TryGetValue(pub, out var a)
                    ? a : new HashSet<string>(StringComparer.Ordinal);

                foreach (var g in genFields.Keys)
                {
                    if (!pubFields.ContainsKey(g))
                    {
                        failures.Add($"{pub.Name}: generated field '{g}' (on {gen.Name}) is NOT modelled by the public DTO");
                    }
                }

                foreach (var p in pubFields.Keys)
                {
                    if (!genFields.ContainsKey(p) && !allow.Contains(p))
                    {
                        failures.Add($"{pub.Name}: public-only field '{p}' is absent from {gen.Name} and not allow-listed");
                    }

                    if (genFields.TryGetValue(p, out var gcat) && gcat != pubFields[p])
                    {
                        failures.Add($"{pub.Name}.{p}: type-category mismatch (public={pubFields[p]}, generated={gcat})");
                    }
                }
            }

            failures.Should().BeEmpty(
                "the public DTOs must cover every generated wire field (spec §6.7). " +
                "Fix the public DTO, or for a deliberate public-only field extend PublicOnlyAllowlist:\n  " +
                string.Join("\n  ", failures));
        }

        // Public wire names/categories come straight from the SDK's STJ source-gen
        // context, so they are exactly what ships on the wire (camelCase policy).
        private static Dictionary<string, string> PublicWireFields(Type t)
        {
            var info = PeakJsonContext.Default.GetTypeInfo(t)
                ?? throw new InvalidOperationException($"{t.Name} is not registered in PeakJsonContext");
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonPropertyInfo p in info.Properties)
            {
                result[p.Name] = Category(p.PropertyType);
            }
            return result;
        }

        // Generated wire names/categories from the Newtonsoft [DataMember] metadata.
        private static Dictionary<string, string> GeneratedWireFields(Type t)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var dm = prop.GetCustomAttribute<DataMemberAttribute>();
                if (dm is null) continue; // only [DataMember] wire fields
                var name = !string.IsNullOrEmpty(dm.Name) ? dm.Name! : CamelCase(prop.Name);
                result[name] = Category(prop.PropertyType);
            }
            return result;
        }

        private static string Category(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            if (t == typeof(string)) return "string";
            if (t == typeof(bool)) return "bool";
            if (t.IsEnum) return "string"; // generated string enums serialise as strings
            if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
                || t == typeof(decimal) || t == typeof(double) || t == typeof(float))
                return "number";
            if (t != typeof(string) && typeof(IEnumerable).IsAssignableFrom(t)) return "array";
            return "object";
        }

        private static string CamelCase(string s) =>
            string.IsNullOrEmpty(s) || char.IsLower(s[0]) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);
    }
}
