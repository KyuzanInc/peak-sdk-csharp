// Ported from upstream-snapshots/peak-sdk-unity/Runtime/AuthenticatedPeakSdk.cs.

using System.Threading;
using System.Threading.Tasks;
using KyuzanInc.Peak.Sdk.Models;
using KyuzanInc.Peak.Sdk.Services;
using KyuzanInc.Peak.Sdk.Storage;
using KyuzanInc.Peak.Sdk.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KyuzanInc.Peak.Sdk
{
    public sealed class AuthenticatedPeakClient
    {
        private readonly PeakClientOptions options;
        private readonly SessionData session;
        private readonly AccountService accountService;
        private readonly PrivateKeyService privateKeyService;

        internal AuthenticatedPeakClient(
            PeakClientOptions options,
            SessionData session,
            IPeakHttpClient httpClient,
            ILoggerFactory? loggerFactory = null)
        {
            this.options = options;
            this.session = session;
            this.accountService = new AccountService(options.ApiUrl, options.ProjectApiKey, session.SessionJwt!, httpClient);
            this.privateKeyService = new PrivateKeyService(
                options.ApiUrl,
                options.ProjectApiKey,
                session.SessionJwt!,
                session.TargetPrivateKey!,
                httpClient,
                this.accountService);
        }

        public AuthenticatedData GetAuthenticatedData()
        {
            if (string.IsNullOrEmpty(session.SessionJwt))
            {
                throw new PeakError(PeakErrorCode.NotAuthenticated, "Authentication data not available");
            }
            var payload = SessionJwt.DecodeSessionJwt(session.SessionJwt!);
            return new AuthenticatedData
            {
                Email = session.Email ?? string.Empty,
                SessionJwt = session.SessionJwt,
                SessionJwtPayload = payload,
            };
        }

        public Task<AccountResponse[]> ListAccountsAsync(CancellationToken cancellationToken = default) =>
            accountService.ListAccountsAsync(cancellationToken);

        public Task<AccountAddressResponse[]> ListAccountAddressesAsync(string accountId, CancellationToken cancellationToken = default) =>
            accountService.ListAccountAddressesAsync(accountId, cancellationToken);

        public Task<AccountResponse?> UpdateAccountDisplayNameAsync(string accountId, string displayName, CancellationToken cancellationToken = default) =>
            accountService.UpdateAccountDisplayNameAsync(accountId, displayName, cancellationToken);

        public Task<InitImportPrivateKeyResult> InitImportPrivateKeyAsync(CancellationToken cancellationToken = default) =>
            privateKeyService.InitImportPrivateKeyAsync(cancellationToken);

        public Task<CompleteImportPrivateKeyResult> CompleteImportPrivateKeyAsync(string encryptedBundle, string chainType, CancellationToken cancellationToken = default) =>
            privateKeyService.CompleteImportPrivateKeyAsync(encryptedBundle, chainType, cancellationToken);

        public Task<ExportPrivateKeyResult> ExportPrivateKeyAsync(string address, string targetPublicKey, CancellationToken cancellationToken = default) =>
            privateKeyService.ExportPrivateKeyAsync(address, targetPublicKey, cancellationToken);
    }
}
