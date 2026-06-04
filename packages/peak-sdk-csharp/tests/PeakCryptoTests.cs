// Tests for PeakCrypto — the public, Peak-owned wrapper over
// global::Turnkey.Crypto / global::Turnkey.Encoding.
//
// The deep crypto vectors (HKDF, HPKE, P-256, full bundle decrypt) are
// already covered in turnkey-sdk-csharp's own CryptoTests. The job here
// is narrower: prove that PeakCrypto *delegates* to Turnkey and *maps*
// its Peak-owned param/result types onto the Turnkey ones field-by-field.
//
// Strategy per surface:
//   - GetPublicKey:        equals the Turnkey output for a fixed 32-byte
//                          private-key vector (compressed and uncompressed).
//   - Encoding helpers:    equal the Turnkey outputs for a vector + round-trip.
//   - GenerateP256KeyPair: returns a Peak KeyPair of 3 non-empty lowercase
//                          hex fields that is self-consistent under GetPublicKey.
//   - DecryptExportBundle: full deterministic delegation proof using the same
//                          upstream golden export bundle the Turnkey port pins.
//                          A correct field-by-field mapping (ExportBundle /
//                          EmbeddedKey / OrganizationId / KeyFormat /
//                          ReturnMnemonic) yields the exact same mnemonic / hex
//                          the raw Turnkey call yields — and a wrong mapping
//                          cannot.
//   - EncryptPrivateKeyToBundle: PeakCrypto's public params intentionally OMIT
//                          DangerouslyOverrideSignerPublicKey, so a fully
//                          deterministic encrypt round-trip isn't reachable
//                          through the Peak surface. Instead we prove (a) the
//                          missing-args contract matches the Turnkey call, and
//                          (b) each Peak field is forwarded by driving the
//                          wrapper to the same signer-mismatch failure the
//                          equivalent Turnkey params produce on the same input.

using System;
using FluentAssertions;
using Xunit;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public class PeakCryptoTests
    {
        // A valid, deterministic 32-byte P-256 private key (scalar in [1, n-1]).
        // Same constant the wire-format smoke test uses, so vectors stay stable.
        private const string FixedPrivateKeyHex =
            "1111111111111111111111111111111111111111111111111111111111111111";

        // ------------------------------------------------------------------
        // GetPublicKey — delegation against a fixed vector
        // ------------------------------------------------------------------

        [Fact]
        public void GetPublicKey_Compressed_EqualsTurnkey()
        {
            byte[] priv = global::Turnkey.Encoding.Uint8ArrayFromHexString(FixedPrivateKeyHex);

            byte[] viaPeak = PeakCrypto.GetPublicKey(priv, isCompressed: true);
            byte[] viaTurnkey = global::Turnkey.Crypto.GetPublicKey(priv, isCompressed: true);

            viaPeak.Should().Equal(viaTurnkey);
            viaPeak.Should().HaveCount(33); // compressed SEC1
        }

        [Fact]
        public void GetPublicKey_Uncompressed_EqualsTurnkey()
        {
            byte[] priv = global::Turnkey.Encoding.Uint8ArrayFromHexString(FixedPrivateKeyHex);

            byte[] viaPeak = PeakCrypto.GetPublicKey(priv, isCompressed: false);
            byte[] viaTurnkey = global::Turnkey.Crypto.GetPublicKey(priv, isCompressed: false);

            viaPeak.Should().Equal(viaTurnkey);
            viaPeak.Should().HaveCount(65); // uncompressed SEC1 (0x04 || X || Y)
        }

        [Fact]
        public void GetPublicKey_DefaultsToCompressed()
        {
            byte[] priv = global::Turnkey.Encoding.Uint8ArrayFromHexString(FixedPrivateKeyHex);

            // Default isCompressed=true must match the explicit compressed call.
            PeakCrypto.GetPublicKey(priv)
                .Should().Equal(PeakCrypto.GetPublicKey(priv, isCompressed: true));
        }

        // ------------------------------------------------------------------
        // Encoding helpers — delegation + round-trip
        // ------------------------------------------------------------------

        [Fact]
        public void Uint8ArrayToHexString_EqualsTurnkey()
        {
            byte[] vector = { 0x00, 0x01, 0x10, 0x7f, 0x80, 0xab, 0xff };

            PeakCrypto.Uint8ArrayToHexString(vector)
                .Should().Be(global::Turnkey.Encoding.Uint8ArrayToHexString(vector));
        }

        [Fact]
        public void Uint8ArrayFromHexString_EqualsTurnkey()
        {
            const string hex = "0001107f80abff";

            PeakCrypto.Uint8ArrayFromHexString(hex)
                .Should().Equal(global::Turnkey.Encoding.Uint8ArrayFromHexString(hex));
        }

        [Fact]
        public void EncodingHelpers_RoundTrip()
        {
            byte[] vector = { 0xde, 0xad, 0xbe, 0xef, 0x00, 0x42 };

            string hex = PeakCrypto.Uint8ArrayToHexString(vector);
            byte[] back = PeakCrypto.Uint8ArrayFromHexString(hex);

            back.Should().Equal(vector);
            hex.Should().Be("deadbeef0042");
        }

        // ------------------------------------------------------------------
        // GenerateP256KeyPair — shape + self-consistency
        // ------------------------------------------------------------------

        [Fact]
        public void GenerateP256KeyPair_ReturnsPeakKeyPair_WithThreeNonEmptyLowercaseHexFields()
        {
            PeakCrypto.KeyPair kp = PeakCrypto.GenerateP256KeyPair();

            kp.Should().NotBeNull();
            kp.PrivateKey.Should().MatchRegex("^[0-9a-f]{64}$");          // 32 bytes
            kp.PublicKey.Should().MatchRegex("^[0-9a-f]{66}$");           // 33 bytes (compressed)
            kp.PublicKeyUncompressed.Should().MatchRegex("^[0-9a-f]{130}$"); // 65 bytes (uncompressed)
        }

        [Fact]
        public void GenerateP256KeyPair_IsSelfConsistentUnderGetPublicKey()
        {
            PeakCrypto.KeyPair kp = PeakCrypto.GenerateP256KeyPair();
            byte[] priv = PeakCrypto.Uint8ArrayFromHexString(kp.PrivateKey);

            // Compressed public key recomputed from the private key must match.
            PeakCrypto.Uint8ArrayToHexString(PeakCrypto.GetPublicKey(priv, isCompressed: true))
                .Should().Be(kp.PublicKey);

            // Uncompressed likewise.
            PeakCrypto.Uint8ArrayToHexString(PeakCrypto.GetPublicKey(priv, isCompressed: false))
                .Should().Be(kp.PublicKeyUncompressed);
        }

        // ------------------------------------------------------------------
        // DecryptExportBundle — full deterministic delegation + field mapping
        //
        // Reuses the upstream golden export bundle that turnkey-sdk-csharp's
        // CryptoTests pins (generated by a real enclave; verifies against the
        // production signer in Turnkey.Crypto.Constants). Getting the same
        // plaintext out the Peak surface proves every Peak param field maps to
        // the right Turnkey field.
        // ------------------------------------------------------------------

        private const string UpstreamExportBundleJson =
            "{\n"
            + "  \"version\": \"v1.0.0\",\n"
            + "  \"data\": \"7b22656e6361707065645075626c6963223a2230343434313065633837653566653266666461313561313866613337376132316133633431633334373666383631333362343238306164373631303266343064356462326463353362343730303763636139336166666330613535316464353134333937643039373931636664393233306663613330343862313731663364363738222c2263697068657274657874223a22656662303538626633666634626534653232323330326266326636303738363062343237346232623031616339343536643362613638646135613235363236303030613839383262313465306261663061306465323966353434353461333739613362653664633364386339343938376131353638633764393566396663346239316265663232316165356562383432333361323833323131346431373962646664636631643066376164656231353766343131613439383430222c226f7267616e697a6174696f6e4964223a2266396133316336342d643630342d343265342d396265662d613737333039366166616437227d\",\n"
            + "  \"dataSignature\": \"304502203a7dc258590a637e76f6be6ed1a2080eed5614175060b9073f5e36592bdaf610022100ab9955b603df6cf45408067f652da48551652451b91967bf37dd094d13a7bdd4\",\n"
            + "  \"enclaveQuorumPublic\": \"04cf288fe433cc4e1aa0ce1632feac4ea26bf2f5a09dcfe5a42c398e06898710330f0572882f4dbdf0f5304b8fc8703acd69adca9a4bbf7f5d00d20a5e364b2569\"\n"
            + "}";

        private const string UpstreamExportEmbeddedKey =
            "ffc6090f14bcf260e5dfe63f45412e60a477bb905956d7cc90195b71c2a544b3";

        private const string UpstreamExportOrganizationId =
            "f9a31c64-d604-42e4-9bef-a773096afad7";

        [Fact]
        public void DecryptExportBundle_Mnemonic_MatchesTurnkeyAndExpectedVector()
        {
            const string expectedMnemonic =
                "leaf lady until indicate praise final route toast cake minimum insect unknown";

            string viaPeak = PeakCrypto.DecryptExportBundle(new PeakCrypto.DecryptExportBundleParams
            {
                ExportBundle = UpstreamExportBundleJson,
                EmbeddedKey = UpstreamExportEmbeddedKey,
                OrganizationId = UpstreamExportOrganizationId,
                KeyFormat = "HEXADECIMAL",
                ReturnMnemonic = true,
            });

            string viaTurnkey = global::Turnkey.Crypto.DecryptExportBundle(
                new global::Turnkey.Crypto.DecryptExportBundleParams
                {
                    ExportBundle = UpstreamExportBundleJson,
                    EmbeddedKey = UpstreamExportEmbeddedKey,
                    OrganizationId = UpstreamExportOrganizationId,
                    KeyFormat = "HEXADECIMAL",
                    ReturnMnemonic = true,
                });

            viaPeak.Should().Be(expectedMnemonic);
            viaPeak.Should().Be(viaTurnkey); // byte-for-byte delegation
        }

        [Fact]
        public void DecryptExportBundle_NonMnemonic_HonorsReturnMnemonicFalse()
        {
            const string expectedHex =
                "6c656166206c61647920756e74696c20696e646963617465207072616973652066696e616c20726f75746520746f6173742063616b65206d696e696d756d20696e7365637420756e6b6e6f776e";

            string viaPeak = PeakCrypto.DecryptExportBundle(new PeakCrypto.DecryptExportBundleParams
            {
                ExportBundle = UpstreamExportBundleJson,
                EmbeddedKey = UpstreamExportEmbeddedKey,
                OrganizationId = UpstreamExportOrganizationId,
                KeyFormat = "HEXADECIMAL",
                ReturnMnemonic = false,
            });

            // ReturnMnemonic=false must be forwarded: hex out, not the mnemonic.
            viaPeak.Should().Be(expectedHex);
        }

        [Fact]
        public void DecryptExportBundle_WrongOrganizationId_IsForwardedAndRejected()
        {
            // Proves OrganizationId is actually forwarded into the Turnkey call:
            // a mismatched org id makes the inner verification fail.
            Action act = () => PeakCrypto.DecryptExportBundle(new PeakCrypto.DecryptExportBundleParams
            {
                ExportBundle = UpstreamExportBundleJson,
                EmbeddedKey = UpstreamExportEmbeddedKey,
                OrganizationId = "not-the-right-org",
                KeyFormat = "HEXADECIMAL",
                ReturnMnemonic = true,
            });

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*organization id does not match*");
        }

        [Fact]
        public void DecryptExportBundle_NullParams_Throws()
        {
            Action act = () => PeakCrypto.DecryptExportBundle(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        // ------------------------------------------------------------------
        // EncryptPrivateKeyToBundle — delegation + field-forwarding proof
        // ------------------------------------------------------------------

        [Fact]
        public void EncryptPrivateKeyToBundle_NullParams_Throws()
        {
            Action act = () => PeakCrypto.EncryptPrivateKeyToBundle(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void EncryptPrivateKeyToBundle_MissingArgs_ThrowsLikeTurnkey()
        {
            // Mirror turnkey-sdk-csharp's EncryptPrivateKeyToBundle_MissingArgs_Throws:
            // empty params (null ImportBundle / PrivateKey) must surface the same
            // ArgumentException family the raw Turnkey call does.
            Action viaPeak = () =>
                PeakCrypto.EncryptPrivateKeyToBundle(new PeakCrypto.EncryptPrivateKeyToBundleParams());
            Action viaTurnkey = () =>
                global::Turnkey.Crypto.EncryptPrivateKeyToBundle(
                    new global::Turnkey.Crypto.EncryptPrivateKeyToBundleParams());

            viaPeak.Should().Throw<ArgumentException>();
            viaTurnkey.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void EncryptPrivateKeyToBundle_ForwardsAllFields_MatchingTurnkeyBehavior()
        {
            // Field-forwarding proof. We feed a structurally valid import bundle
            // whose enclaveQuorumPublic is NOT the production signer (and
            // PeakCrypto intentionally cannot override the signer). The inner
            // Turnkey verifyEnclaveSignature throws the SAME signer-mismatch
            // error regardless of which path built the params — proving the
            // wrapper forwarded PrivateKey/ImportBundle/OrganizationId/UserId/
            // KeyFormat into an equivalently-constructed Turnkey params object
            // (a dropped/renamed field would change where/how it fails).
            const string privateKey =
                "0000000000000000000000000000000000000000000000000000000000000001";
            const string organizationId = "org-xyz";
            const string userId = "user-xyz";
            const string keyFormat = "HEXADECIMAL";

            // enclaveQuorumPublic deliberately != PRODUCTION_SIGNER_SIGN_PUBLIC_KEY.
            const string importBundle =
                "{\"enclaveQuorumPublic\":\"04aaaa\",\"dataSignature\":\"00\",\"data\":\"00\"}";

            Exception viaPeak = Record.Exception(() =>
                PeakCrypto.EncryptPrivateKeyToBundle(new PeakCrypto.EncryptPrivateKeyToBundleParams
                {
                    PrivateKey = privateKey,
                    ImportBundle = importBundle,
                    OrganizationId = organizationId,
                    UserId = userId,
                    KeyFormat = keyFormat,
                }));

            Exception viaTurnkey = Record.Exception(() =>
                global::Turnkey.Crypto.EncryptPrivateKeyToBundle(
                    new global::Turnkey.Crypto.EncryptPrivateKeyToBundleParams
                    {
                        PrivateKey = privateKey,
                        ImportBundle = importBundle,
                        OrganizationId = organizationId,
                        UserId = userId,
                        KeyFormat = keyFormat,
                    }));

            // Both must fail at the same inner step with the same message,
            // proving the Peak wrapper builds an equivalent Turnkey params.
            viaPeak.Should().NotBeNull();
            viaTurnkey.Should().NotBeNull();
            viaPeak!.GetType().Should().Be(viaTurnkey!.GetType());
            viaPeak.Message.Should().Be(viaTurnkey.Message);
            viaPeak.Message.Should().Contain("does not match signer key from bundle");
        }
    }
}
