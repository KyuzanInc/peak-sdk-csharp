using System;

namespace Peak.Models.Api
{
    /// <summary>
    /// Entity DTO for account source information.
    /// Mirrors AccountSourceResponseDto in peak-server database response DTOs.
    /// </summary>
    [Serializable]
    public class AccountSourceResponse
    {
        /// <summary>
        /// Account source ID
        /// </summary>
        public string id;

        /// <summary>
        /// User ID
        /// </summary>
        public string userId;

        /// <summary>
        /// Origin project ID
        /// </summary>
        public string originProjectId;

        /// <summary>
        /// Source type: "recovery-phrase" or "private-key"
        /// </summary>
        public string sourceType;

        /// <summary>
        /// Creation method: "imported" or "created"
        /// </summary>
        public string creationMethod;

        /// <summary>
        /// Turnkey resource ID (HD wallet ID or private key ID)
        /// </summary>
        public string turnkeyResourceId;

        /// <summary>
        /// User-defined display name (nullable)
        /// </summary>
        public string displayName;
    }
}
