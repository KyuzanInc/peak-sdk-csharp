using System;
using System.Text;
using FluentAssertions;
using Org.BouncyCastle.Math;
using Xunit;

namespace KyuzanInc.Turnkey.Sdk.Tests
{
    public class CryptoTests
    {
        // --- ModSqrt ---

        [Fact]
        public void ModSqrt_NistP256_Prime_Works()
        {
            var p = new BigInteger(CryptoConstants.P256_P);
            // 4 is a quadratic residue mod any odd prime; sqrt(4) = 2.
            var four = new BigInteger("4");
            var sqrt = Crypto.Math.ModSqrt(four, p);
            sqrt.Multiply(sqrt).Mod(p).Should().Be(four);
        }

        [Fact]
        public void ModSqrt_NonPositiveP_Throws()
        {
            Action act = () => Crypto.Math.ModSqrt(BigInteger.One, BigInteger.Zero);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ModSqrt_NonResidue_Throws()
        {
            // p ≡ 3 (mod 4), so we know the fast path runs. For most random x the result
            // will check out; but if we feed in a value whose modular-square doesn't
            // round-trip, we should throw. The straightforward way to construct a forced
            // mismatch is to pick p=7 (3 mod 4) and x=3, where 3 is a non-residue.
            var p = new BigInteger("7");
            var x = new BigInteger("3");
            Action act = () => Crypto.Math.ModSqrt(x, p);
            act.Should().Throw<InvalidOperationException>();
        }

        // --- HKDF (RFC 5869) ---

        // RFC 5869 Test Case 1 (Basic test case with SHA-256)
        [Fact]
        public void Hkdf_Rfc5869_Tc1()
        {
            var ikm = HexToBytes("0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b");
            var salt = HexToBytes("000102030405060708090a0b0c");
            var info = HexToBytes("f0f1f2f3f4f5f6f7f8f9");
            var prkExpected = HexToBytes("077709362c2e32df0ddc3f0dc47bba6390b6c73bb50f9c3122ec844ad7c2b3e5");
            var okmExpected = HexToBytes("3cb25f25faacd57a90434f64d0362f2a2d2d0a90cf1a5a4c5db02d56ecc4c5bf34007208d5b887185865");

            Crypto.Hkdf.Extract(salt, ikm).Should().Equal(prkExpected);
            Crypto.Hkdf.Expand(prkExpected, info, 42).Should().Equal(okmExpected);
        }

        // RFC 5869 Test Case 2 (longer inputs/outputs)
        [Fact]
        public void Hkdf_Rfc5869_Tc2()
        {
            var ikm = HexToBytes("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f404142434445464748494a4b4c4d4e4f");
            var salt = HexToBytes("606162636465666768696a6b6c6d6e6f707172737475767778797a7b7c7d7e7f808182838485868788898a8b8c8d8e8f909192939495969798999a9b9c9d9e9fa0a1a2a3a4a5a6a7a8a9aaabacadaeaf");
            var info = HexToBytes("b0b1b2b3b4b5b6b7b8b9babbbcbdbebfc0c1c2c3c4c5c6c7c8c9cacbcccdcecfd0d1d2d3d4d5d6d7d8d9dadbdcdddedfe0e1e2e3e4e5e6e7e8e9eaebecedeeeff0f1f2f3f4f5f6f7f8f9fafbfcfdfeff");
            var prkExpected = HexToBytes("06a6b88c5853361a06104c9ceb35b45cef760014904671014a193f40c15fc244");
            var okmExpected = HexToBytes("b11e398dc80327a1c8e7f78c596a49344f012eda2d4efad8a050cc4c19afa97c59045a99cac7827271cb41c65e590e09da3275600c2f09b8367793a9aca3db71cc30c58179ec3e87c14c01d5c1f3434f1d87");

            Crypto.Hkdf.Extract(salt, ikm).Should().Equal(prkExpected);
            Crypto.Hkdf.Expand(prkExpected, info, 82).Should().Equal(okmExpected);
        }

        // RFC 5869 Test Case 3 (zero-length salt and info)
        [Fact]
        public void Hkdf_Rfc5869_Tc3()
        {
            var ikm = HexToBytes("0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b");
            var salt = new byte[0];
            var info = new byte[0];
            var prkExpected = HexToBytes("19ef24a32c717b167f33a91d6f648bdf96596776afdb6377ac434c1c293ccb04");
            var okmExpected = HexToBytes("8da4e775a563c18f715f802a063c5a31b8a11f5c5ee1879ec3454e5f3c738d2d9d201395faa4b61a96c8");

            Crypto.Hkdf.Extract(salt, ikm).Should().Equal(prkExpected);
            Crypto.Hkdf.Expand(prkExpected, info, 42).Should().Equal(okmExpected);
        }

        [Fact]
        public void Hkdf_Expand_RejectsExcessiveLength()
        {
            var prk = new byte[32];
            Action act = () => Crypto.Hkdf.Expand(prk, null, 256 * 32);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Hkdf_Expand_RejectsShortPrk()
        {
            Action act = () => Crypto.Hkdf.Expand(new byte[16], null, 16);
            act.Should().Throw<ArgumentException>();
        }

        // --- P-256 key derivation ---

        [Fact]
        public void GetPublicKey_Compressed_HasCorrectLengthAndPrefix()
        {
            var pair = Crypto.GenerateP256KeyPair();
            var pub = Encoding.Uint8ArrayFromHexString(pair.PublicKey!);
            pub.Length.Should().Be(33);
            pub[0].Should().BeOneOf((byte)0x02, (byte)0x03);
        }

        [Fact]
        public void GetPublicKey_Uncompressed_HasCorrectLengthAndPrefix()
        {
            var pair = Crypto.GenerateP256KeyPair();
            var pub = Encoding.Uint8ArrayFromHexString(pair.PublicKeyUncompressed!);
            pub.Length.Should().Be(65);
            pub[0].Should().Be(0x04);
        }

        [Fact]
        public void CompressUncompress_RoundTrip()
        {
            var pair = Crypto.GenerateP256KeyPair();
            var uncompressed = Encoding.Uint8ArrayFromHexString(pair.PublicKeyUncompressed!);
            var compressed = Crypto.CompressRawPublicKey(uncompressed);
            var roundtrip = Crypto.UncompressRawPublicKey(compressed);
            roundtrip.Should().Equal(uncompressed);
        }

        [Fact]
        public void UncompressRawPublicKey_InvalidPrefix_Throws()
        {
            var bad = new byte[33];
            bad[0] = 0x04;
            Action act = () => Crypto.UncompressRawPublicKey(bad);
            act.Should().Throw<ArgumentException>().WithMessage("*invalid prefix*");
        }

        [Fact]
        public void UncompressRawPublicKey_WrongSize_Throws()
        {
            Action act = () => Crypto.UncompressRawPublicKey(new byte[32]);
            act.Should().Throw<ArgumentException>().WithMessage("*size*");
        }

        [Fact]
        public void CompressRawPublicKey_RejectsBadPrefix()
        {
            var bad = new byte[65];
            bad[0] = 0x05;
            Action act = () => Crypto.CompressRawPublicKey(bad);
            act.Should().Throw<ArgumentException>();
        }

        // --- HPKE round-trip ---

        [Fact]
        public void Hpke_RoundTrip_ArbitraryPlaintext()
        {
            // Generate a receiver key pair, encrypt to the receiver's public key,
            // and decrypt with the receiver's private key.
            var receiver = Crypto.GenerateP256KeyPair();
            var receiverPubUncompressed = Encoding.Uint8ArrayFromHexString(receiver.PublicKeyUncompressed!);
            var message = System.Text.Encoding.UTF8.GetBytes("HPKE round-trip test 🐉");

            var encrypted = Crypto.HpkeEncrypt(new Crypto.HpkeEncryptParams
            {
                PlainTextBuf = message,
                TargetKeyBuf = receiverPubUncompressed,
            });

            // Output is: compressedSenderPub (33 bytes) || ciphertext (msg.Length + 16 GCM tag)
            encrypted.Length.Should().Be(33 + message.Length + 16);

            var compressedSender = new byte[33];
            Array.Copy(encrypted, 0, compressedSender, 0, 33);
            var senderUncompressed = Crypto.UncompressRawPublicKey(compressedSender);

            var ciphertext = new byte[encrypted.Length - 33];
            Array.Copy(encrypted, 33, ciphertext, 0, ciphertext.Length);

            var decrypted = Crypto.HpkeDecrypt(new Crypto.HpkeDecryptParams
            {
                CiphertextBuf = ciphertext,
                EncappedKeyBuf = senderUncompressed,
                ReceiverPriv = receiver.PrivateKey,
            });

            decrypted.Should().Equal(message);
        }

        [Fact]
        public void Hpke_RoundTrip_EmptyPlaintext()
        {
            var receiver = Crypto.GenerateP256KeyPair();
            var receiverPubUncompressed = Encoding.Uint8ArrayFromHexString(receiver.PublicKeyUncompressed!);

            var encrypted = Crypto.HpkeEncrypt(new Crypto.HpkeEncryptParams
            {
                PlainTextBuf = Array.Empty<byte>(),
                TargetKeyBuf = receiverPubUncompressed,
            });

            // 33 byte compressed pub + 16 byte GCM tag
            encrypted.Length.Should().Be(33 + 16);

            var compressedSender = new byte[33];
            Array.Copy(encrypted, 0, compressedSender, 0, 33);
            var senderUncompressed = Crypto.UncompressRawPublicKey(compressedSender);

            var ciphertext = new byte[encrypted.Length - 33];
            Array.Copy(encrypted, 33, ciphertext, 0, ciphertext.Length);

            var decrypted = Crypto.HpkeDecrypt(new Crypto.HpkeDecryptParams
            {
                CiphertextBuf = ciphertext,
                EncappedKeyBuf = senderUncompressed,
                ReceiverPriv = receiver.PrivateKey,
            });

            decrypted.Length.Should().Be(0);
        }

        [Fact]
        public void Hpke_TamperedCiphertext_FailsAuth()
        {
            var receiver = Crypto.GenerateP256KeyPair();
            var receiverPubUncompressed = Encoding.Uint8ArrayFromHexString(receiver.PublicKeyUncompressed!);
            var message = System.Text.Encoding.UTF8.GetBytes("authenticate me");

            var encrypted = Crypto.HpkeEncrypt(new Crypto.HpkeEncryptParams
            {
                PlainTextBuf = message,
                TargetKeyBuf = receiverPubUncompressed,
            });

            // Tamper with the ciphertext (flip a bit past the encapsulated key prefix).
            encrypted[35] ^= 0x01;

            var compressedSender = new byte[33];
            Array.Copy(encrypted, 0, compressedSender, 0, 33);
            var senderUncompressed = Crypto.UncompressRawPublicKey(compressedSender);

            var ciphertext = new byte[encrypted.Length - 33];
            Array.Copy(encrypted, 33, ciphertext, 0, ciphertext.Length);

            Action act = () => Crypto.HpkeDecrypt(new Crypto.HpkeDecryptParams
            {
                CiphertextBuf = ciphertext,
                EncappedKeyBuf = senderUncompressed,
                ReceiverPriv = receiver.PrivateKey,
            });

            act.Should().Throw<Exception>();
        }

        // --- DecryptCredentialBundle structural validation ---

        [Fact]
        public void DecryptCredentialBundle_TooShortBundle_Throws()
        {
            // Encode 32 bytes (one shorter than 33 = compressed-key length) via Base58Check.
            var data = new byte[32];
            using var sha = System.Security.Cryptography.SHA256.Create();
            var checksum = sha.ComputeHash(sha.ComputeHash(data));
            var withChecksum = new byte[36];
            Array.Copy(data, 0, withChecksum, 0, 32);
            Array.Copy(checksum, 0, withChecksum, 32, 4);
            var b58 = Encoding.Base58Encode(withChecksum);

            Action act = () => Crypto.DecryptCredentialBundle(b58, "0000000000000000000000000000000000000000000000000000000000000001");
            act.Should().Throw<Exception>().WithMessage("*Bundle size*too low*");
        }

        // --- JWT signature ---

        [Fact]
        public void VerifySessionJwtSignature_ReturnsFalse_OnMalformedJwt()
        {
            Crypto.VerifySessionJwtSignature("not.a.jwt.with.too.many.dots").Should().BeFalse();
            Crypto.VerifySessionJwtSignature("missing-dots").Should().BeFalse();
        }

        [Fact]
        public void VerifySessionJwtSignature_ReturnsFalse_OnWrongSignatureLength()
        {
            // Header and payload as base64url; signature is base64url("not-64-bytes")
            var header = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"alg\":\"ES256\"}"))
                .Replace("+", "-").Replace("/", "_").TrimEnd('=');
            var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{}"))
                .Replace("+", "-").Replace("/", "_").TrimEnd('=');
            var sig = Convert.ToBase64String(new byte[10])
                .Replace("+", "-").Replace("/", "_").TrimEnd('=');
            var jwt = $"{header}.{payload}.{sig}";
            Crypto.VerifySessionJwtSignature(jwt).Should().BeFalse();
        }

        // --- FormatHpkeBuf shape ---

        [Fact]
        public void FormatHpkeBuf_ProducesCamelCaseJson()
        {
            // Round-trip an HPKE encrypt, hand it to FormatHpkeBuf, and parse the JSON.
            var receiver = Crypto.GenerateP256KeyPair();
            var pubUncompressed = Encoding.Uint8ArrayFromHexString(receiver.PublicKeyUncompressed!);
            var encrypted = Crypto.HpkeEncrypt(new Crypto.HpkeEncryptParams
            {
                PlainTextBuf = new byte[] { 0xCA, 0xFE },
                TargetKeyBuf = pubUncompressed,
            });

            var json = Crypto.FormatHpkeBuf(encrypted);
            // Expect lowerCamelCase keys to match the JS output.
            json.Should().Contain("encappedPublic");
            json.Should().Contain("ciphertext");
            json.Should().NotContain("EncappedPublic");

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            doc.RootElement.GetProperty("encappedPublic").GetString().Should().NotBeNullOrEmpty();
            doc.RootElement.GetProperty("ciphertext").GetString().Should().NotBeNullOrEmpty();
        }

        // --- Helpers ---

        private static byte[] HexToBytes(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
    }
}
