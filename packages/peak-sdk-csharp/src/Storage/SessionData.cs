namespace KyuzanInc.Peak.Sdk.Storage
{
    /// <summary>
    /// Session payload persisted via <see cref="IStorage"/> by the OTP login
    /// flow and read back by <c>PeakClient.Authenticate</c>. Matches the
    /// Unity port's <c>SessionStorage.SessionData</c> field set.
    /// </summary>
    public sealed class SessionData
    {
        public string? Email { get; set; }
        public string? SessionJwt { get; set; }
        public string? TargetPrivateKey { get; set; }
        public string? TargetPublicKey { get; set; }
        public long SavedAt { get; set; }

        /// <summary>The single key under which the SessionData JSON is persisted.</summary>
        public const string StorageKey = "kyuzan.peak.sdk.session";
    }
}
