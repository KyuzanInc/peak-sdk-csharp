namespace KyuzanInc.Peak.Sdk.Models
{
    // Internal response wrapper for POST /accounts/update-display-name, which the
    // server shapes as { "account": {...} }. Internal so it does not touch the
    // public surface (the public method returns AccountResponse?). Registered in
    // PeakJsonContext for AOT-safe source-gen deserialization.
    internal sealed class UpdateAccountDisplayNameEnvelope
    {
        public AccountResponse? Account { get; set; }
    }
}
