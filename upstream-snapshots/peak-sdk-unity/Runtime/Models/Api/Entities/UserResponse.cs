using System;

namespace Peak.Models.Api
{
    /// <summary>
    /// Entity DTO for user information.
    /// Mirrors UserResponseDto in peak-server database response DTOs.
    /// </summary>
    [Serializable]
    public class UserResponse
    {
        /// <summary>
        /// User ID
        /// </summary>
        public string id;

        /// <summary>
        /// User email address
        /// </summary>
        public string email;

        /// <summary>
        /// Origin project ID
        /// </summary>
        public string originProjectId;

        /// <summary>
        /// Turnkey sub-organization ID
        /// </summary>
        public string turnkeySubOrgId;

        /// <summary>
        /// Turnkey root user ID
        /// </summary>
        public string turnkeyRootUserId;

        /// <summary>
        /// Deletion approval status ("none", "approved", "in_progress")
        /// </summary>
        public string deletionStatus;

        /// <summary>
        /// Whether the user is authenticated
        /// </summary>
        public bool isAuthenticated;
    }
}
