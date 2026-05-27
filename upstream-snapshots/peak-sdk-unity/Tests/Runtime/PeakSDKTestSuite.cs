using NUnit.Framework;
using Peak;
using Peak.Utils;
using Peak.Exceptions;
using UnityEngine;

namespace Peak.Tests
{
    /// <summary>
    /// Test suite entry point for Peak SDK
    /// This class ensures all critical functionality is tested before marking features as complete
    /// </summary>
    public class PeakSDKTestSuite
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Debug.Log("=== Peak SDK Test Suite Starting ===");
            Debug.Log($"Unity Version: {Application.unityVersion}");
            Debug.Log($"Platform: {Application.platform}");
            Debug.Log($"Mobile Platform: {Application.isMobilePlatform}");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            Debug.Log("=== Peak SDK Test Suite Completed ===");
        }

        /// <summary>
        /// Validates that all core SDK initialization components are working
        /// This test ensures the SDK can be initialized with proper validation
        /// </summary>
        [Test]
        [Category("Integration")]
        public void CoreSDKInitialization_IntegrationTest()
        {
            // Arrange
            var validOptions = new SdkOptions
            {
                ProjectApiKey = "integration-test-api-key",
                ApiUrl = "https://api.peak.test.com"
            };

            // Act
            PeakSdk sdk = null;
            Assert.DoesNotThrow(() => {
                sdk = PeakSdk.Initialize(validOptions);
            }, "SDK initialization should not throw with valid options");

            // Assert
            Assert.IsNotNull(sdk, "SDK instance should be created");
            Assert.IsInstanceOf<PeakSdk>(sdk, "Should return PeakSdk instance");
        }

        /// <summary>
        /// Validates that error handling works as expected across all components
        /// </summary>
        [Test]
        [Category("Integration")]
        public void ErrorHandling_IntegrationTest()
        {
            // Test that proper error codes are returned for various failure scenarios
            
            // Null configuration
            var nullConfigException = Assert.Throws<SdkException>(() => {
                PeakSdk.Initialize(null);
            });
            Assert.AreEqual(SdkErrorCodes.INVALID_CONFIG, nullConfigException.ErrorCode);

            // Invalid URL
            var invalidUrlException = Assert.Throws<SdkException>(() => {
                PeakSdk.Initialize(new SdkOptions { ProjectApiKey = "test", ApiUrl = "invalid" });
            });
            Assert.AreEqual(SdkErrorCodes.INVALID_CONFIG, invalidUrlException.ErrorCode);

            Debug.Log("Error handling integration test passed");
        }

        /// <summary>
        /// Validates that platform detection works correctly
        /// </summary>
        [Test]
        [Category("Integration")]
        public void PlatformDetection_IntegrationTest()
        {
            // Test platform helper methods
            string platformName = PlatformHelper.GetPlatformName();
            bool isMobile = PlatformHelper.IsMobilePlatform();
            bool isIOS = PlatformHelper.IsIOSPlatform();
            bool isAndroid = PlatformHelper.IsAndroidPlatform();

            Assert.IsNotNull(platformName, "Platform name should not be null");
            Assert.IsNotEmpty(platformName, "Platform name should not be empty");

            // Logic validation
            if (isIOS || isAndroid)
            {
                Assert.IsTrue(isMobile, "If iOS or Android, should be considered mobile");
            }

            Assert.IsFalse(isIOS && isAndroid, "Cannot be both iOS and Android");

            Debug.Log($"Platform detection test passed - Platform: {platformName}, Mobile: {isMobile}");
        }
    }
}
