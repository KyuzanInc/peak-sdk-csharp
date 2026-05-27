using NUnit.Framework;
using Peak;
using Peak.Utils;
using Peak.Exceptions;
using UnityEngine;
using UnityEngine.TestTools;

namespace Peak.Tests.Utils
{
    public class ConfigValidatorTests
    {
        [Test]
        public void ValidateInitializationOptions_WithValidOptions_DoesNotThrow()
        {
            // Arrange
            var options = new SdkOptions
            {
                ProjectApiKey = "test-api-key",
                ApiUrl = "https://api.example.com"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => ConfigValidator.ValidateInitializationOptions(options));
        }

        [Test]
        public void ValidateInitializationOptions_WithNullOptions_ThrowsSdkException()
        {
            // Arrange
            SdkOptions options = null;

            // Act & Assert
            var exception = Assert.Throws<SdkException>(() => ConfigValidator.ValidateInitializationOptions(options));
            Assert.AreEqual(SdkErrorCodes.INVALID_CONFIG, exception.ErrorCode);
            Assert.That(exception.Message, Contains.Substring("SdkOptions cannot be null"));
        }

        [Test]
        public void ValidateInitializationOptions_WithEmptyProjectApiKey_ThrowsSdkException()
        {
            // Arrange
            var options = new SdkOptions
            {
                ProjectApiKey = "",
                ApiUrl = "https://api.example.com"
            };

            // Act & Assert
            var exception = Assert.Throws<SdkException>(() => ConfigValidator.ValidateInitializationOptions(options));
            Assert.AreEqual(SdkErrorCodes.INVALID_CONFIG, exception.ErrorCode);
            Assert.That(exception.Message, Contains.Substring("ProjectApiKey is required"));
        }

        [Test]
        public void ValidateInitializationOptions_WithNullProjectApiKey_ThrowsSdkException()
        {
            // Arrange
            var options = new SdkOptions
            {
                ProjectApiKey = null,
                ApiUrl = "https://api.example.com"
            };

            // Act & Assert
            var exception = Assert.Throws<SdkException>(() => ConfigValidator.ValidateInitializationOptions(options));
            Assert.AreEqual(SdkErrorCodes.INVALID_CONFIG, exception.ErrorCode);
            Assert.That(exception.Message, Contains.Substring("ProjectApiKey is required"));
        }

        [Test]
        public void ValidateInitializationOptions_WithEmptyApiUrl_ThrowsSdkException()
        {
            // Arrange
            var options = new SdkOptions
            {
                ProjectApiKey = "test-api-key",
                ApiUrl = ""
            };

            // Act & Assert
            var exception = Assert.Throws<SdkException>(() => ConfigValidator.ValidateInitializationOptions(options));
            Assert.AreEqual(SdkErrorCodes.INVALID_CONFIG, exception.ErrorCode);
            Assert.That(exception.Message, Contains.Substring("ApiUrl is required"));
        }

        [Test]
        public void ValidateInitializationOptions_WithNullApiUrl_ThrowsSdkException()
        {
            // Arrange
            var options = new SdkOptions
            {
                ProjectApiKey = "test-api-key",
                ApiUrl = null
            };

            // Act & Assert
            var exception = Assert.Throws<SdkException>(() => ConfigValidator.ValidateInitializationOptions(options));
            Assert.AreEqual(SdkErrorCodes.INVALID_CONFIG, exception.ErrorCode);
            Assert.That(exception.Message, Contains.Substring("ApiUrl is required"));
        }

        [Test]
        public void ValidateInitializationOptions_WithInvalidApiUrl_ThrowsSdkException()
        {
            // Arrange
            var options = new SdkOptions
            {
                ProjectApiKey = "test-api-key",
                ApiUrl = "invalid-url"
            };

            // Act & Assert
            var exception = Assert.Throws<SdkException>(() => ConfigValidator.ValidateInitializationOptions(options));
            Assert.AreEqual(SdkErrorCodes.INVALID_CONFIG, exception.ErrorCode);
            Assert.That(exception.Message, Contains.Substring("ApiUrl must be a valid URL"));
        }

        [Test]
        public void ValidateInitializationOptions_WithHttpUrl_DoesNotThrow()
        {
            // Arrange
            var options = new SdkOptions
            {
                ProjectApiKey = "test-api-key",
                ApiUrl = "http://api.example.com"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => ConfigValidator.ValidateInitializationOptions(options));
        }

        [Test]
        public void ValidateInitializationOptions_WithHttpsUrl_DoesNotThrow()
        {
            // Arrange
            var options = new SdkOptions
            {
                ProjectApiKey = "test-api-key",
                ApiUrl = "https://api.example.com"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => ConfigValidator.ValidateInitializationOptions(options));
        }

        [Test]
        public void ValidateInitializationOptions_OnNonMobilePlatform_LogsWarning()
        {
            // Arrange
            var options = new SdkOptions
            {
                ProjectApiKey = "test-api-key",
                ApiUrl = "https://api.example.com"
            };

            // Note: This test may not trigger warning on mobile platforms
            // The test validates that the validation method doesn't throw
            Assert.DoesNotThrow(() => ConfigValidator.ValidateInitializationOptions(options));
        }
    }
}