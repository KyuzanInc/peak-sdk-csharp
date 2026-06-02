using System;
using System.Text;

namespace KyuzanInc.Peak.Sdk.Tests
{
    // Builds an unsigned JWT whose payload carries every claim
    // SessionJwt.DecodeSessionJwt requires (exp, organization_id, public_key,
    // session_type, user_id — all non-empty, exp non-zero). DecodeSessionJwt does
    // not verify the signature or expiry, so a dummy signature + far-future exp
    // are fine.
    internal static class FakeJwt
    {
        public static string ValidSession(string orgId = "org-1", string userId = "user-1")
        {
            static string B64Url(string s) =>
                Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var header = B64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}");
            var payload = B64Url(
                "{\"exp\":9999999999," +
                $"\"organization_id\":\"{orgId}\"," +
                "\"public_key\":\"pub-key\"," +
                "\"session_type\":\"api\"," +
                $"\"user_id\":\"{userId}\"}}");
            return $"{header}.{payload}.sig";
        }
    }
}
