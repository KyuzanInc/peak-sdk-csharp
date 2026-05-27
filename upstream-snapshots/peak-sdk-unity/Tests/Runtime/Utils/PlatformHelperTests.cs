using NUnit.Framework;
using Peak.Utils;
using UnityEngine;

namespace Peak.Tests.Utils
{
    public class PlatformHelperTests
    {
        [Test]
        public void GetPlatformName_ReturnsNonEmptyString()
        {
            // Act
            string platformName = PlatformHelper.GetPlatformName();

            // Assert
            Assert.IsNotNull(platformName);
            Assert.IsNotEmpty(platformName);
        }

        [Test]
        public void IsMobilePlatform_ReturnsBoolean()
        {
            // Act
            bool isMobile = PlatformHelper.IsMobilePlatform();

            // Assert
            Assert.IsTrue(isMobile is true or false); // Just ensure it returns a boolean
        }

        [Test]
        public void IsIOSPlatform_ReturnsBoolean()
        {
            // Act
            bool isIOS = PlatformHelper.IsIOSPlatform();

            // Assert
            Assert.IsTrue(isIOS is true or false); // Just ensure it returns a boolean
        }

        [Test]
        public void IsAndroidPlatform_ReturnsBoolean()
        {
            // Act
            bool isAndroid = PlatformHelper.IsAndroidPlatform();

            // Assert
            Assert.IsTrue(isAndroid is true or false); // Just ensure it returns a boolean
        }

        [Test]
        public void PlatformMethods_ConsistentResults()
        {
            // Arrange & Act
            bool isMobile = PlatformHelper.IsMobilePlatform();
            bool isIOS = PlatformHelper.IsIOSPlatform();
            bool isAndroid = PlatformHelper.IsAndroidPlatform();
            string platformName = PlatformHelper.GetPlatformName();

            // Assert - if it's iOS or Android, it should be mobile
            if (isIOS || isAndroid)
            {
                Assert.IsTrue(isMobile, "If platform is iOS or Android, it should be considered mobile");
            }

            // Assert - platform name should match boolean checks
            if (isIOS)
            {
                Assert.AreEqual("iOS", platformName, "Platform name should be 'iOS' when IsIOSPlatform returns true");
            }
            else if (isAndroid)
            {
                Assert.AreEqual("Android", platformName, "Platform name should be 'Android' when IsAndroidPlatform returns true");
            }

            // Assert - can't be both iOS and Android
            Assert.IsFalse(isIOS && isAndroid, "Platform cannot be both iOS and Android");
        }

#if UNITY_IOS
        [Test]
        public void OnIOSPlatform_IOSMethodsReturnTrue()
        {
            // Act & Assert
            Assert.IsTrue(PlatformHelper.IsIOSPlatform());
            Assert.IsTrue(PlatformHelper.IsMobilePlatform());
            Assert.AreEqual("iOS", PlatformHelper.GetPlatformName());
            Assert.IsFalse(PlatformHelper.IsAndroidPlatform());
        }
#endif

#if UNITY_ANDROID
        [Test]
        public void OnAndroidPlatform_AndroidMethodsReturnTrue()
        {
            // Act & Assert
            Assert.IsTrue(PlatformHelper.IsAndroidPlatform());
            Assert.IsTrue(PlatformHelper.IsMobilePlatform());
            Assert.AreEqual("Android", PlatformHelper.GetPlatformName());
            Assert.IsFalse(PlatformHelper.IsIOSPlatform());
        }
#endif
    }
}