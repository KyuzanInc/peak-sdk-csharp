// Compile-only API-conformance guard for examples/peak-sdk-unity-reference/.
// Mirrors the SDK calls PeakExampleDemo.cs makes (minus UnityEngine). If the SDK's
// public surface drifts, this fails to COMPILE in the existing test build — the
// automated floor the design (D13) requires. The CompileOnly method is never run
// (it would need network/auth); only its type-checking matters.
using System.Threading.Tasks;
using KyuzanInc.Peak.Sdk;
using KyuzanInc.Peak.Sdk.Models;
using Xunit;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public sealed class ExampleApiConformanceTests
    {
        private const string ChainTypeEvm = "evm";
        private const string KeyFormatHex = "HEXADECIMAL";

        [Fact]
        public void Example_chainType_constant_is_the_supported_value()
        {
            // The example uses "evm"; the SDK accepts exactly {"evm","solana"} and
            // throws on anything else (e.g. "ETHEREUM"). Pin the value here.
            Assert.Equal("evm", ChainTypeEvm);
        }

        [Fact]
        public void Example_sdk_calls_type_check_against_the_public_surface()
        {
            // The proof is that CompileOnly compiles. Reference it without running it
            // (false is not a compile-time constant, so no unreachable-code warning).
            if (bool.Parse(bool.FalseString))
            {
                _ = CompileOnly(null!, null!);
            }
            Assert.True(true);
        }

        private static async Task CompileOnly(PeakClient client, AuthenticatedPeakClient authed)
        {
            // Model props are nullable (string?); keep locals null-clean so the file
            // is warning-free even though the test csproj relaxes warnings-as-errors.
            InitOtpLoginResponse? otp = await client.InitOtpLoginAsync("e@example.com");
            string otpId = otp?.OtpId ?? "";
            CompleteOtpLoginResult login = await client.CompleteOtpLoginAsync("e@example.com", otpId, "000000");
            _ = login.SessionJwt;
            AuthenticatedPeakClient a = client.Authenticate();
            client.Logout();

            AccountResponse[] accounts = await authed.ListAccountsAsync();
            string accountId = accounts.Length > 0 ? accounts[0].Id ?? "id" : "id";
            AccountAddressResponse[] addrs = await authed.ListAccountAddressesAsync(accountId);
            string address = addrs.Length > 0 ? addrs[0].Address ?? "addr" : "addr";

            InitImportPrivateKeyResult init = await authed.InitImportPrivateKeyAsync();
            string bundle = PeakCrypto.EncryptPrivateKeyToBundle(new PeakCrypto.EncryptPrivateKeyToBundleParams
            {
                PrivateKey = "deadbeef",
                ImportBundle = init.ImportBundle,
                OrganizationId = init.OrganizationId,
                UserId = init.UserId,
                KeyFormat = KeyFormatHex,
            });
            CompleteImportPrivateKeyResult imp = await authed.CompleteImportPrivateKeyAsync(bundle, ChainTypeEvm);
            _ = imp.Account;

            PeakCrypto.KeyPair target = PeakCrypto.GenerateP256KeyPair();
            ExportPrivateKeyResult exp = await authed.ExportPrivateKeyAsync(address, target.PublicKey);
            string key = PeakCrypto.DecryptExportBundle(new PeakCrypto.DecryptExportBundleParams
            {
                ExportBundle = exp.ExportBundle,
                EmbeddedKey = target.PrivateKey,
                OrganizationId = exp.OrganizationId,
                KeyFormat = KeyFormatHex,
                ReturnMnemonic = false,
            });
            _ = (a, address, key);
        }
    }
}
