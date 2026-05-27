using System;
using System.Collections;
using NUnit.Framework;
using Peak.Utils;
using Turnkey;
using UnityEngine;
using UnityEngine.TestTools;

namespace Peak.Tests
{
    public class TurnkeyTests
    {
        #region Turnkey Tests

        [Test]
        public void Turnkey_GenerateP256KeyPair_GeneratesValidKeyPair()
        {
            // Act
            var keyPair = TurnkeyUtils.GenerateP256KeyPair();

            // Assert
            Assert.IsNotNull(keyPair);
            Assert.IsNotNull(keyPair.privateKey);
            Assert.IsNotNull(keyPair.publicKey);
            Assert.IsNotNull(keyPair.publicKeyUncompressed);

            // Private key should be 64 characters (32 bytes in hex)
            Assert.AreEqual(64, keyPair.privateKey.Length);

            // Compressed public key should be 66 characters (33 bytes in hex)
            Assert.AreEqual(66, keyPair.publicKey.Length);

            // Uncompressed public key should be 130 characters (65 bytes in hex)
            Assert.AreEqual(130, keyPair.publicKeyUncompressed.Length);

            // Compressed public key should start with 02 or 03
            Assert.IsTrue(keyPair.publicKey.StartsWith("02") || keyPair.publicKey.StartsWith("03"));

            // Uncompressed public key should start with 04
            Assert.IsTrue(keyPair.publicKeyUncompressed.StartsWith("04"));

            Debug.Log($"Generated P256 Key Pair:");
            Debug.Log($"Private Key: {keyPair.privateKey}");
            Debug.Log($"Public Key (compressed): {keyPair.publicKey}");
            Debug.Log($"Public Key (uncompressed): {keyPair.publicKeyUncompressed}");
        }

        [Test]
        public void Turnkey_GetPublicKey_DerivesCorrectPublicKey()
        {
            // Arrange
            var keyPair = TurnkeyUtils.GenerateP256KeyPair();
            var privateKeyBytes = TurnkeyUtils.Uint8ArrayFromHexString(keyPair.privateKey);

            // Act
            var derivedPublicKeyCompressed = TurnkeyUtils.GetPublicKey(privateKeyBytes, true);
            var derivedPublicKeyUncompressed = TurnkeyUtils.GetPublicKey(privateKeyBytes, false);

            // Assert
            var derivedCompressedHex = TurnkeyUtils.Uint8ArrayToHexString(derivedPublicKeyCompressed);
            var derivedUncompressedHex = TurnkeyUtils.Uint8ArrayToHexString(derivedPublicKeyUncompressed);

            Assert.AreEqual(keyPair.publicKey, derivedCompressedHex);
            Assert.AreEqual(keyPair.publicKeyUncompressed, derivedUncompressedHex);
        }

        [Test]
        public void Turnkey_HexEncoding_RoundTrip()
        {
            // Arrange
            var originalData = new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF };

            // Act
            var hex = TurnkeyUtils.Uint8ArrayToHexString(originalData);
            var decoded = TurnkeyUtils.Uint8ArrayFromHexString(hex);

            // Assert
            Assert.AreEqual("0123456789abcdef", hex);
            CollectionAssert.AreEqual(originalData, decoded);
        }

        [Test]
        public void Turnkey_GenerateMultipleKeyPairs_ProducesUniqueKeys()
        {
            // Act
            var keyPair1 = TurnkeyUtils.GenerateP256KeyPair();
            var keyPair2 = TurnkeyUtils.GenerateP256KeyPair();
            var keyPair3 = TurnkeyUtils.GenerateP256KeyPair();

            // Assert - All keys should be unique
            Assert.AreNotEqual(keyPair1.privateKey, keyPair2.privateKey);
            Assert.AreNotEqual(keyPair1.privateKey, keyPair3.privateKey);
            Assert.AreNotEqual(keyPair2.privateKey, keyPair3.privateKey);

            Assert.AreNotEqual(keyPair1.publicKey, keyPair2.publicKey);
            Assert.AreNotEqual(keyPair1.publicKey, keyPair3.publicKey);
            Assert.AreNotEqual(keyPair2.publicKey, keyPair3.publicKey);
        }

        [Test]
        public void Turnkey_Uint8ArrayFromHexString_InvalidHex_ThrowsException()
        {
            // Test invalid hex characters
            Assert.Throws<FormatException>(() =>
                TurnkeyUtils.Uint8ArrayFromHexString("invalid-hex"));

            Assert.Throws<FormatException>(() =>
                TurnkeyUtils.Uint8ArrayFromHexString("gg"));

            Assert.Throws<FormatException>(() =>
                TurnkeyUtils.Uint8ArrayFromHexString("12345g"));
        }

        [Test]
        public void Turnkey_Uint8ArrayFromHexString_OddLength_ThrowsException()
        {
            // Test odd length hex string
            Assert.Throws<ArgumentException>(() =>
                TurnkeyUtils.Uint8ArrayFromHexString("123"));

            Assert.Throws<ArgumentException>(() =>
                TurnkeyUtils.Uint8ArrayFromHexString("12345"));
        }

        [Test]
        public void Turnkey_Uint8ArrayFromHexString_EmptyAndNull()
        {
            // Test empty string
            var emptyResult = TurnkeyUtils.Uint8ArrayFromHexString("");
            Assert.AreEqual(0, emptyResult.Length);

            // Test null string
            var nullResult = TurnkeyUtils.Uint8ArrayFromHexString(null);
            Assert.AreEqual(0, nullResult.Length);
        }

        [Test]
        public void Turnkey_Uint8ArrayToHexString_NullInput()
        {
            // Test null input
            var result = TurnkeyUtils.Uint8ArrayToHexString(null);
            Assert.AreEqual("", result);
        }

        [Test]
        public void Turnkey_HexEncoding_VariousLengths()
        {
            // Test various byte array lengths
            TestHexRoundTrip(new byte[0]); // Empty
            TestHexRoundTrip(new byte[] { 0x00 }); // Single zero
            TestHexRoundTrip(new byte[] { 0xFF }); // Single max
            TestHexRoundTrip(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 }); // Multiple
            TestHexRoundTrip(new byte[32]); // 32 bytes (typical key size)
            TestHexRoundTrip(new byte[65]); // 65 bytes (uncompressed public key)
        }

        private void TestHexRoundTrip(byte[] original)
        {
            var hex = TurnkeyUtils.Uint8ArrayToHexString(original);
            var decoded = TurnkeyUtils.Uint8ArrayFromHexString(hex);
            CollectionAssert.AreEqual(original, decoded);
        }

        [Test]
        public void Turnkey_GetPublicKey_WithShortPrivateKey()
        {
            // Test with private key shorter than 32 bytes (should be padded)
            var shortPrivateKey = new byte[] { 0x01, 0x02, 0x03 };

            // This should not throw
            var publicKey = TurnkeyUtils.GetPublicKey(shortPrivateKey, true);

            Assert.IsNotNull(publicKey);
            Assert.AreEqual(33, publicKey.Length); // Compressed public key
        }

        #endregion

        #region TurnkeyUtils.GetTurnkeyHttpClient Tests

        [Test]
        public void Turnkey_GetTurnkeyHttpClient_CreatesClientFromValidPrivateKey()
        {
            // Arrange - generate a valid P256 key pair
            var keyPair = TurnkeyUtils.GenerateP256KeyPair();

            // Act - create HTTP client from private key (OTP session flow)
            var httpClient = TurnkeyUtils.GetTurnkeyHttpClient(keyPair.privateKey);

            // Assert
            Assert.IsNotNull(httpClient);
        }

        [Test]
        public void Turnkey_GetTurnkeyHttpClient_ThrowsOnNullPrivateKey()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                TurnkeyUtils.GetTurnkeyHttpClient(null));
        }

        [Test]
        public void Turnkey_GetTurnkeyHttpClient_ThrowsOnEmptyPrivateKey()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                TurnkeyUtils.GetTurnkeyHttpClient(""));
        }

        [Test]
        public void Turnkey_GetTurnkeyHttpClient_ThrowsOnInvalidHex()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                TurnkeyUtils.GetTurnkeyHttpClient("invalid-hex-string"));
        }

        #endregion

        #region TurnkeyUtils.VerifySessionJwtSignature Tests

        [Test]
        public void Turnkey_VerifySessionJwtSignature_ReturnsFalseForInvalidJwt()
        {
            // Arrange - invalid JWT format
            var invalidJwt = "not-a-valid-jwt";

            // Act
            var result = TurnkeyUtils.VerifySessionJwtSignature(invalidJwt);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void Turnkey_VerifySessionJwtSignature_ReturnsFalseForEmptyJwt()
        {
            // Act & Assert
            Assert.IsFalse(TurnkeyUtils.VerifySessionJwtSignature(""));
            Assert.IsFalse(TurnkeyUtils.VerifySessionJwtSignature(null));
        }

        [Test]
        public void Turnkey_VerifySessionJwtSignature_ReturnsFalseForMalformedJwt()
        {
            // Arrange - JWT with wrong number of parts
            var malformedJwt = "header.payload"; // Missing signature part

            // Act
            var result = TurnkeyUtils.VerifySessionJwtSignature(malformedJwt);

            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region Integration Tests

        [UnityTest]
        public IEnumerator TurnkeyIntegration_KeyGenerationAndSigning_Success()
        {
            yield return null;

            // Generate key pair
            var keyPair = TurnkeyUtils.GenerateP256KeyPair();
            Assert.IsNotNull(keyPair);

            // Create stamper
            var stamper = new ApiKeyStamper(keyPair.publicKey, keyPair.privateKey);
            Assert.IsNotNull(stamper);

            // Create multiple stamps with different payloads
            var payloads = new[]
            {
                "{\"action\":\"login\"}",
                "{\"action\":\"logout\"}",
                "{\"data\":{\"nested\":true}}"
            };

            foreach (var payload in payloads)
            {
                var stamp = stamper.Stamp(payload);
                var stampValue = stamp.stampHeaderValue;
                Assert.IsNotNull(stamp);
                Assert.IsTrue(stampValue.Length > 0);

                // Verify base64url format
                Assert.IsFalse(stampValue.Contains("=", StringComparison.Ordinal));
                Assert.IsFalse(stampValue.Contains("+", StringComparison.Ordinal));
                Assert.IsFalse(stampValue.Contains("/", StringComparison.Ordinal));
            }

            Debug.Log("Integration test: Key generation and signing completed successfully");
        }

        [Test]
        public void TurnkeyIntegration_HexEncodingConsistency()
        {
            // Generate multiple key pairs and verify hex encoding consistency
            for (int i = 0; i < 5; i++)
            {
                var keyPair = TurnkeyUtils.GenerateP256KeyPair();

                // Decode and re-encode private key
                var privateKeyBytes = TurnkeyUtils.Uint8ArrayFromHexString(keyPair.privateKey);
                var reEncodedPrivateKey = TurnkeyUtils.Uint8ArrayToHexString(privateKeyBytes);
                Assert.AreEqual(keyPair.privateKey, reEncodedPrivateKey);

                // Decode and re-encode public keys
                var publicKeyBytes = TurnkeyUtils.Uint8ArrayFromHexString(keyPair.publicKey);
                var reEncodedPublicKey = TurnkeyUtils.Uint8ArrayToHexString(publicKeyBytes);
                Assert.AreEqual(keyPair.publicKey, reEncodedPublicKey);
            }
        }

        #endregion

        #region Performance Tests

        [UnityTest]
        public IEnumerator TurnkeyPerformance_KeyGeneration_Benchmark()
        {
            yield return null;

            const int iterations = 10;
            var startTime = Time.realtimeSinceStartup;

            for (int i = 0; i < iterations; i++)
            {
                var keyPair = TurnkeyUtils.GenerateP256KeyPair();
                Assert.IsNotNull(keyPair);
            }

            var totalTime = Time.realtimeSinceStartup - startTime;
            var avgTime = totalTime / iterations;

            Debug.Log($"Performance: Generated {iterations} key pairs in {totalTime:F3}s");
            Debug.Log($"Average time per key pair: {avgTime * 1000:F2}ms");

            // Ensure reasonable performance (less than 100ms per key)
            Assert.Less(avgTime, 0.1f, "Key generation is too slow");
        }

        #endregion
    }
}
