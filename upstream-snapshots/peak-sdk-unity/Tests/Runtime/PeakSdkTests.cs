using System;
using NUnit.Framework;
using Peak;
using Peak.Utils;
using Peak.Exceptions;
using UnityEngine;
using UnityEngine.TestTools;

namespace Peak.Tests
{
    public class PeakSdkTests
    {
        private SdkOptions validOptions;

        [SetUp]
        public void SetUp()
        {
            validOptions = new SdkOptions
            {
                ProjectApiKey = "test-api-key-12345",
                ApiUrl = "https://api.peak.example.com"
            };
        }

        [TearDown]
        public void TearDown()
        {
            SessionStorage.Clear();
        }

        [Test]
        public void Initialize_WithValidOptions_ReturnsSDKInstance()
        {
            // Act
            var sdk = PeakSdk.Initialize(validOptions);

            // Assert
            Assert.IsNotNull(sdk);
            Assert.IsInstanceOf<PeakSdk>(sdk);
        }

        [Test]
        public void Initialize_WithNullOptions_ThrowsSdkException()
        {
            // Act & Assert
            var exception = Assert.Throws<SdkException>(() => PeakSdk.Initialize(null));
            Assert.AreEqual(SdkErrorCodes.INVALID_CONFIG, exception.ErrorCode);
        }

        [Test]
        public void Initialize_WithInvalidApiKey_ThrowsSdkException()
        {
            // Arrange
            var invalidOptions = new SdkOptions
            {
                ProjectApiKey = "",
                ApiUrl = "https://api.peak.example.com"
            };

            // Act & Assert
            var exception = Assert.Throws<SdkException>(() => PeakSdk.Initialize(invalidOptions));
            Assert.AreEqual(SdkErrorCodes.INVALID_CONFIG, exception.ErrorCode);
        }

        [Test]
        public void Initialize_WithInvalidUrl_ThrowsSdkException()
        {
            // Arrange
            var invalidOptions = new SdkOptions
            {
                ProjectApiKey = "test-api-key",
                ApiUrl = "invalid-url"
            };

            // Act & Assert
            var exception = Assert.Throws<SdkException>(() => PeakSdk.Initialize(invalidOptions));
            Assert.AreEqual(SdkErrorCodes.INVALID_CONFIG, exception.ErrorCode);
        }

        [Test]
        public void Initialize_LogsPlatformInformation()
        {
            // Act
            var sdk = PeakSdk.Initialize(validOptions);

            // Assert
            Assert.IsNotNull(sdk);
        }

        [Test]
        public void Authenticate_WithoutStoredSession_ThrowsSdkException()
        {
            // Arrange
            var sdk = PeakSdk.Initialize(validOptions);

            // Act & Assert
            var exception = Assert.Throws<SdkException>(() => sdk.Authenticate());
            Assert.AreEqual(SdkErrorCodes.AUTHENTICATION_FAILED, exception.ErrorCode);
        }

        [Test]
        public void Authenticate_WithStoredSession_ReturnsAuthenticatedSDK()
        {
            // Arrange
            var sdk = PeakSdk.Initialize(validOptions);

            SessionStorage.Save(new SessionStorage.SessionData
            {
                email = "test@example.com",
                sessionJwt = "test-session-jwt",
                targetPrivateKey = "test-target-private-key",
                targetPublicKey = "test-target-public-key",
                savedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });

            // Act
            var authenticatedSdk = sdk.Authenticate();

            // Assert
            Assert.IsNotNull(authenticatedSdk);
            Assert.IsInstanceOf<AuthenticatedPeakSdk>(authenticatedSdk);
        }

        [Test]
        public void Authenticate_WithInvalidStoredSession_ClearsSessionAndThrows()
        {
            // Arrange
            var sdk = PeakSdk.Initialize(validOptions);

            SessionStorage.Save(new SessionStorage.SessionData
            {
                email = "test@example.com",
                sessionJwt = string.Empty,
                targetPrivateKey = string.Empty,
                savedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });

            Assert.IsTrue(SessionStorage.HasSession());

            // Act
            var exception = Assert.Throws<SdkException>(() => sdk.Authenticate());

            // Assert
            Assert.AreEqual(SdkErrorCodes.AUTHENTICATION_FAILED, exception.ErrorCode);
            Assert.IsFalse(SessionStorage.HasSession());
        }

    }
}
