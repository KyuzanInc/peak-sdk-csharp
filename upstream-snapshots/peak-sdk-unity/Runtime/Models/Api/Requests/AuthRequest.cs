using System;

namespace Peak.Models.Api
{
    /// <summary>
    /// Request DTO for user signup.
    /// </summary>
    [Serializable]
    public class SignupRequest
    {
        public string email;

        public SignupRequest(string email)
        {
            this.email = email;
        }
    }

    /// <summary>
    /// Request DTO for initiating OTP login.
    /// </summary>
    [Serializable]
    public class InitOtpLoginRequest
    {
        public string email;

        public InitOtpLoginRequest(string email)
        {
            this.email = email;
        }
    }

    /// <summary>
    /// Request DTO for completing OTP login.
    /// </summary>
    [Serializable]
    public class CompleteOtpLoginRequest
    {
        public string email;
        public string otpId;
        public string otpCode;
        public string targetPublicKey;
        /// <summary>
        /// If true (default), creates user when missing. If false, returns error when user not found.
        /// </summary>
        public bool signup;

        public CompleteOtpLoginRequest(string email, string otpId, string otpCode, string targetPublicKey, bool signup = true)
        {
            this.email = email;
            this.otpId = otpId;
            this.otpCode = otpCode;
            this.targetPublicKey = targetPublicKey;
            this.signup = signup;
        }
    }
}
