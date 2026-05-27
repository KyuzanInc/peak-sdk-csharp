using System;
using FluentAssertions;
using Xunit;

namespace KyuzanInc.Turnkey.Sdk.Tests
{
    public class EncodingTests
    {
        // --- Hex round-trip ---

        [Theory]
        [InlineData("", new byte[] { })]
        [InlineData("00", new byte[] { 0x00 })]
        [InlineData("deadbeef", new byte[] { 0xde, 0xad, 0xbe, 0xef })]
        [InlineData("ff", new byte[] { 0xff })]
        public void Hex_Roundtrip_Matches(string hex, byte[] expected)
        {
            if (hex.Length == 0)
            {
                Encoding.Uint8ArrayToHexString(expected).Should().BeEmpty();
                return;
            }

            var bytes = Encoding.Uint8ArrayFromHexString(hex);
            bytes.Should().Equal(expected);
            Encoding.Uint8ArrayToHexString(bytes).Should().Be(hex.ToLowerInvariant());
        }

        [Fact]
        public void Hex_UppercaseAccepted_LowercaseOnDecode()
        {
            var bytes = Encoding.Uint8ArrayFromHexString("DEADBEEF");
            Encoding.Uint8ArrayToHexString(bytes).Should().Be("deadbeef");
        }

        [Fact]
        public void Hex_InvalidChar_Throws()
        {
            Action act = () => Encoding.Uint8ArrayFromHexString("zz");
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Hex_OddLength_Throws()
        {
            Action act = () => Encoding.Uint8ArrayFromHexString("abc");
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Hex_PaddingWithLength()
        {
            var bytes = Encoding.Uint8ArrayFromHexString("01", 4);
            bytes.Should().Equal(new byte[] { 0x00, 0x00, 0x00, 0x01 });
        }

        [Fact]
        public void Hex_OverflowLength_Throws()
        {
            Action act = () => Encoding.Uint8ArrayFromHexString("deadbeef", 2);
            act.Should().Throw<ArgumentException>();
        }

        // --- Base58 ---

        // Bitcoin BIP-13 known vectors (subset)
        [Theory]
        [InlineData("00", "1")]                        // single zero byte → "1"
        [InlineData("61", "2g")]                       // 'a' → "2g"
        [InlineData("626262", "a3gV")]                 // 'bbb' → "a3gV"
        [InlineData("636363", "aPEr")]                 // 'ccc' → "aPEr"
        public void Base58_KnownVectors_Encode(string hex, string expected)
        {
            var bytes = Encoding.Uint8ArrayFromHexString(hex);
            Encoding.Base58Encode(bytes).Should().Be(expected);
        }

        [Theory]
        [InlineData("00", "1")]
        [InlineData("61", "2g")]
        [InlineData("626262", "a3gV")]
        [InlineData("636363", "aPEr")]
        public void Base58_KnownVectors_Decode(string expectedHex, string b58)
        {
            var bytes = Encoding.Base58Decode(b58);
            Encoding.Uint8ArrayToHexString(bytes).Should().Be(expectedHex);
        }

        [Fact]
        public void Base58_LeadingZeros_RoundTrip()
        {
            var bytes = new byte[] { 0x00, 0x00, 0x01 };
            var b58 = Encoding.Base58Encode(bytes);
            Encoding.Base58Decode(b58).Should().Equal(bytes);
        }

        [Fact]
        public void Base58_InvalidChar_Throws()
        {
            Action act = () => Encoding.Base58Decode("invalid0OIl");
            act.Should().Throw<ArgumentException>();
        }

        // --- Base58Check ---

        [Fact]
        public void Base58Check_RoundTrip_Decodes_Once()
        {
            // Encode a payload with double-SHA256 checksum manually, then decode.
            var data = new byte[] { 0x01, 0x02, 0x03 };
            using var sha = System.Security.Cryptography.SHA256.Create();
            var checksum = sha.ComputeHash(sha.ComputeHash(data));
            var withChecksum = new byte[data.Length + 4];
            Array.Copy(data, 0, withChecksum, 0, data.Length);
            Array.Copy(checksum, 0, withChecksum, data.Length, 4);
            var b58check = Encoding.Base58Encode(withChecksum);

            var decoded = Encoding.Base58CheckDecode(b58check);
            decoded.Should().Equal(data);
        }

        [Fact]
        public void Base58Check_BadChecksum_Throws()
        {
            // Encode a valid Base58Check and corrupt the last byte before re-encoding.
            var data = new byte[] { 0xAA };
            using var sha = System.Security.Cryptography.SHA256.Create();
            var checksum = sha.ComputeHash(sha.ComputeHash(data));
            var withChecksum = new byte[data.Length + 4];
            Array.Copy(data, 0, withChecksum, 0, data.Length);
            Array.Copy(checksum, 0, withChecksum, data.Length, 4);
            withChecksum[withChecksum.Length - 1] ^= 0x01; // flip a bit
            var corrupted = Encoding.Base58Encode(withChecksum);

            Action act = () => Encoding.Base58CheckDecode(corrupted);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Base58Check_TooShort_Throws()
        {
            // "1" decodes to a single zero byte — below the 4-byte checksum minimum.
            Action act = () => Encoding.Base58CheckDecode("1");
            act.Should().Throw<ArgumentException>();
        }

        // --- Misc helpers ---

        [Fact]
        public void ConcatUint8Arrays_NullSafe()
        {
            var result = Encoding.ConcatUint8Arrays(new byte[] { 1, 2 }, null!, new byte[] { 3 });
            result.Should().Equal(new byte[] { 1, 2, 3 });
        }

        [Fact]
        public void Uint8ArrayToString_Utf8RoundTrip()
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes("hello 🐉");
            Encoding.Uint8ArrayToString(bytes).Should().Be("hello 🐉");
        }
    }
}
