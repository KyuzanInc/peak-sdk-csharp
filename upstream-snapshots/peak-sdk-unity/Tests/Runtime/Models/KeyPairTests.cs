using NUnit.Framework;
using Peak.Models.Sdk;

namespace Peak.Tests.Models
{
    public class KeyPairTests
    {
        [Test]
        public void KeyPair_Constructor_SetsPropertiesCorrectly()
        {
            // Arrange
            string publicKey = "04a1b2c3d4e5f6...public_key_data";
            string privateKey = "private_key_data_12345";

            // Act
            var keyPair = new KeyPair(publicKey, privateKey);

            // Assert
            Assert.AreEqual(publicKey, keyPair.publicKey);
            Assert.AreEqual(privateKey, keyPair.privateKey);
        }

        [Test]
        public void KeyPair_WithNullValues_SetsNullProperties()
        {
            // Act
            var keyPair = new KeyPair(null, null);

            // Assert
            Assert.IsNull(keyPair.publicKey);
            Assert.IsNull(keyPair.privateKey);
        }

        [Test]
        public void KeyPair_WithEmptyValues_SetsEmptyProperties()
        {
            // Act
            var keyPair = new KeyPair("", "");

            // Assert
            Assert.AreEqual("", keyPair.publicKey);
            Assert.AreEqual("", keyPair.privateKey);
        }

        [Test]
        public void KeyPair_IsSerializable()
        {
            // This test ensures the [Serializable] attribute works correctly
            // Arrange
            var keyPair = new KeyPair("public-key-test", "private-key-test");

            // Act & Assert - if this compiles and runs, serialization works
            Assert.IsNotNull(keyPair);
            Assert.IsTrue(keyPair.GetType().IsSerializable);
        }
    }
}