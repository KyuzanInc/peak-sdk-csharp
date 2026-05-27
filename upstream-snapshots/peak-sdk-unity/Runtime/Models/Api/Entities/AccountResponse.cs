using System;

namespace Peak.Models.Api
{
    /// <summary>
    /// Entity DTO for account information.
    /// Mirrors AccountResponseDto in peak-server database response DTOs.
    /// </summary>
    [Serializable]
    public class AccountResponse
    {
        /// <summary>
        /// Account ID
        /// </summary>
        public string id;

        /// <summary>
        /// User ID
        /// </summary>
        public string userId;

        /// <summary>
        /// Account source ID
        /// </summary>
        public string accountSourceId;

        /// <summary>
        /// Account index within the account source
        /// </summary>
        public int accountIndex;

        /// <summary>
        /// Origin project ID
        /// </summary>
        public string originProjectId;

        /// <summary>
        /// User-defined display name (nullable)
        /// </summary>
        public string displayName;
    }
}
