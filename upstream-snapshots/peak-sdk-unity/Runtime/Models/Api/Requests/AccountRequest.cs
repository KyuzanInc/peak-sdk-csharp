using System;

namespace Peak.Models.Api
{
    /// <summary>
    /// Request DTO for updating account display name.
    /// </summary>
    [Serializable]
    public class UpdateAccountDisplayNameRequest
    {
        public string accountId;
        public string displayName;

        public UpdateAccountDisplayNameRequest(string accountId, string displayName)
        {
            this.accountId = accountId;
            this.displayName = displayName;
        }
    }
}
