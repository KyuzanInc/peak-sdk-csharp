using FluentAssertions;
using PublicApiGenerator;
using Xunit;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class PublicSurfaceBaselineTests
    {
        // Enforces the D13 invariant — no internal generated-client type leaks onto
        // the SDK public surface — plus a presence sanity check for the intended
        // surface. Deliberately NOT a byte-for-byte committed snapshot:
        // PublicApiGenerator output varies with the .NET SDK / Roslyn patch
        // (nullable + compiler-generated attributes), so a pinned snapshot is
        // fragile across local vs CI runners (it fails the moment their SDK patch
        // differs). The invariant asserted here is exactly what the design
        // requires; full-surface diffs are covered by code review.
        [Fact]
        public void PublicSurface_HasNoGeneratedTypes()
        {
            var api = typeof(PeakClient).Assembly.GeneratePublicApi();

            // D13: the internal generated client (namespace
            // KyuzanInc.Peak.PublicApiClient.*) must never appear on the public
            // surface. Since issue #18 the SDK does not reference the generated
            // client at all (STJ-only response path), so this stays trivially true.
            api.Should().NotContain("PublicApiClient");

            // Sanity: the intended public surface is present (guards against an
            // accidental wholesale removal or namespace move).
            api.Should().Contain("class PeakClient");
            api.Should().Contain("namespace KyuzanInc.Peak.Sdk.Models");
            api.Should().Contain("class AccountResponse");
            api.Should().Contain("interface IPeakHttpClient");

            // The public, Peak-owned import/export crypto wrapper (and its nested
            // param/result types) must be on the surface so consumers — notably
            // the Unity adapter — can do client-side crypto without referencing
            // Turnkey.* directly.
            api.Should().Contain("class PeakCrypto");
            api.Should().Contain("class KeyPair");
            api.Should().Contain("class EncryptPrivateKeyToBundleParams");
            api.Should().Contain("class DecryptExportBundleParams");

            // The thin wrapper must NOT re-expose Turnkey's test-only signer
            // override on the Peak surface (the Peak param types omit it). This
            // is the PeakCrypto-specific leakage guard: PeakCrypto maps onto its
            // own Peak-owned KeyPair / *Params types, so it adds no new Turnkey
            // type to the surface. (Some Turnkey.* types are referenced
            // elsewhere on the existing surface — e.g. the AuthService
            // keyPairFactory and the import/export request envelopes — which is
            // a separate, pre-existing concern, not introduced here.)
            api.Should().NotContain("DangerouslyOverrideSignerPublicKey");

            // The internal update-display-name wrapper must NOT be public.
            api.Should().NotContain("UpdateAccountDisplayNameEnvelope");
        }
    }
}
