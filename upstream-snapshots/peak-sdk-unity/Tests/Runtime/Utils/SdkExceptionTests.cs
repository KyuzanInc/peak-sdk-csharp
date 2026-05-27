using System;
using NUnit.Framework;
using Peak.Exceptions;

namespace Peak.Tests.Utils
{
    public class SdkExceptionTests
    {
        [Test]
        public void SdkException_WithErrorCodeAndMessage_SetsPropertiesCorrectly()
        {
            // Arrange
            string errorCode = SdkErrorCodes.INVALID_CONFIG;
            string message = "Test error message";

            // Act
            var exception = new SdkException(errorCode, message);

            // Assert
            Assert.AreEqual(errorCode, exception.ErrorCode);
            Assert.AreEqual(message, exception.Message);
            Assert.IsNull(exception.InnerException);
        }

        [Test]
        public void SdkException_WithErrorCodeMessageAndInnerException_SetsPropertiesCorrectly()
        {
            // Arrange
            string errorCode = SdkErrorCodes.NETWORK_ERROR;
            string message = "Network operation failed";
            var innerException = new ArgumentException("Inner exception message");

            // Act
            var exception = new SdkException(errorCode, message, innerException);

            // Assert
            Assert.AreEqual(errorCode, exception.ErrorCode);
            Assert.AreEqual(message, exception.Message);
            Assert.AreEqual(innerException, exception.InnerException);
        }

        [Test]
        public void SdkErrorCodes_HaveExpectedValues()
        {
            // Assert
            Assert.AreEqual("INVALID_CONFIG", SdkErrorCodes.INVALID_CONFIG);
            Assert.AreEqual("PLATFORM_NOT_SUPPORTED", SdkErrorCodes.PLATFORM_NOT_SUPPORTED);
            Assert.AreEqual("INITIALIZATION_FAILED", SdkErrorCodes.INITIALIZATION_FAILED);
            Assert.AreEqual("NETWORK_ERROR", SdkErrorCodes.NETWORK_ERROR);
            Assert.AreEqual("AUTHENTICATION_FAILED", SdkErrorCodes.AUTHENTICATION_FAILED);
        }

        [Test]
        public void SdkException_IsInstanceOfException()
        {
            // Arrange & Act
            var exception = new SdkException(SdkErrorCodes.INVALID_CONFIG, "Test message");

            // Assert
            Assert.IsInstanceOf<Exception>(exception);
        }

        [Test]
        public void SdkException_CanBeCaught()
        {
            // Arrange
            bool exceptionCaught = false;
            string errorCode = SdkErrorCodes.AUTHENTICATION_FAILED;
            string message = "Authentication failed";

            try
            {
                // Act
                throw new SdkException(errorCode, message);
            }
            catch (SdkException ex)
            {
                // Assert
                exceptionCaught = true;
                Assert.AreEqual(errorCode, ex.ErrorCode);
                Assert.AreEqual(message, ex.Message);
            }

            Assert.IsTrue(exceptionCaught, "SdkException should have been caught");
        }
    }
}