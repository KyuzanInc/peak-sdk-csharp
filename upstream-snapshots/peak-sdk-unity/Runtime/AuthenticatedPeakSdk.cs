using System;
using Cysharp.Threading.Tasks;
using Peak.Models.Api;
using Peak.Models.Sdk;
using Peak.Services.Authenticated;
using Peak.Utils;
using Peak.Exceptions;

namespace Peak
{
    /// <summary>
    /// Provides authenticated operations for Peak SDK requiring user credentials.
    /// </summary>
    /// <remarks>
    /// This class handles authenticated wallet operations that require a valid session.
    /// Instances are created via the <see cref="PeakSdk.Authenticate()"/> method
    /// after successful OTP login. Profile management and transaction-signing helpers
    /// will be added in future revisions; for now they are placeholders.
    ///
    /// All methods in this class require valid session credentials and will fail
    /// if the session has expired or is invalid.
    /// </remarks>
    /// <example>
    /// <code>
    /// // First, complete the OTP login flow
    /// var sdk = PeakSdk.Initialize();
    /// var otpResult = await sdk.InitOtpLoginAsync("user@example.com");
    /// await sdk.CompleteOtpLoginAsync("user@example.com", otpResult.OtpId, "123456");
    ///
    /// // Create authenticated SDK instance
    /// var authSdk = sdk.Authenticate();
    ///
    /// // Use authenticated methods
    /// var accounts = await authSdk.ListAccountsAsync();
    /// var initResult = await authSdk.InitImportPrivateKeyAsync();
    /// // ... encrypt and complete import
    /// </code>
    /// </example>
    public class AuthenticatedPeakSdk
    {
        private readonly SdkOptions sdkOptions;
        private readonly AuthenticationOptions authOptions;
        private readonly PrivateKeyService privateKeyService;
        private readonly AccountService accountService;

        public AuthenticatedPeakSdk(SdkOptions sdkOptions, AuthenticationOptions authOptions)
        {
            this.sdkOptions = sdkOptions;
            this.authOptions = authOptions;
            this.privateKeyService = new PrivateKeyService(
                sdkOptions.ApiUrl,
                sdkOptions.ProjectApiKey,
                authOptions.SessionJwt,
                authOptions.TargetPrivateKey);
            this.accountService = new AccountService(
                sdkOptions.ApiUrl,
                sdkOptions.ProjectApiKey,
                authOptions.SessionJwt);
        }

        /// <summary>
        /// Gets the authenticated user's data including email, JWT, and decoded payload.
        /// Aligned with getAuthenticatedData() in peak-sdk-browser and peak-sdk-node.
        /// </summary>
        /// <returns>
        /// An AuthenticatedData containing the email, sessionJwt,
        /// and the decoded sessionJwtPayload with Turnkey credentials.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when authentication options are not available.
        /// </exception>
        /// <remarks>
        /// This method provides convenient access to all authentication data without
        /// requiring manual JWT decoding. The returned sessionJwtPayload includes
        /// Turnkey credentials (TurnkeySubOrgId, TurnkeyUserId), Expiry, and PublicKey.
        /// </remarks>
        /// <example>
        /// <code>
        /// var authSdk = sdk.Authenticate();
        /// var authData = authSdk.GetAuthenticatedData();
        /// Debug.Log($"Logged in as: {authData.email}");
        /// Debug.Log($"Turnkey Org ID: {authData.sessionJwtPayload.TurnkeySubOrgId}");
        /// </code>
        /// </example>
        public AuthenticatedData GetAuthenticatedData()
        {
            if (authOptions == null || string.IsNullOrEmpty(authOptions.SessionJwt))
            {
                throw new InvalidOperationException("Authentication data not available");
            }

            var sessionJwtPayload = SessionJwt.DecodeSessionJwt(authOptions.SessionJwt);

            return new AuthenticatedData
            {
                email = authOptions.Email ?? string.Empty,
                sessionJwt = authOptions.SessionJwt,
                sessionJwtPayload = sessionJwtPayload
            };
        }

        /// <summary>
        /// Retrieves all accounts associated with the authenticated user.
        /// Aligned with listAccounts() in peak-sdk-browser.
        /// </summary>
        /// <returns>
        /// A task that resolves to an array of AccountResponse objects
        /// containing account information.
        /// </returns>
        /// <exception cref="Peak.Exceptions.HttpException">
        /// Thrown when the request fails due to network errors or invalid session.
        /// </exception>
        /// <remarks>
        /// Note: Addresses are NOT included in the response for performance reasons.
        /// Use ListAccountAddressesAsync() to get addresses for a specific account.
        /// </remarks>
        /// <example>
        /// <code>
        /// var authSdk = sdk.Authenticate();
        ///
        /// var accounts = await authSdk.ListAccountsAsync();
        /// foreach (var account in accounts)
        /// {
        ///     Debug.Log($"Account: {account.id}, Name: {account.displayName}");
        /// }
        /// </code>
        /// </example>
        public UniTask<AccountResponse[]> ListAccountsAsync()
        {
            return accountService.ListAccountsAsync();
        }

        /// <summary>
        /// Retrieves all addresses for a specific account.
        /// Aligned with listAccountAddresses() in peak-sdk-browser.
        /// </summary>
        /// <param name="accountId">The account ID to list addresses for.</param>
        /// <returns>
        /// A task that resolves to an array of AccountAddressResponse objects
        /// containing address information for the specified account.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when accountId is null or empty.
        /// </exception>
        /// <exception cref="Peak.Exceptions.HttpException">
        /// Thrown when the request fails due to network errors or invalid session.
        /// </exception>
        /// <example>
        /// <code>
        /// var authSdk = sdk.Authenticate();
        ///
        /// var accounts = await authSdk.ListAccountsAsync();
        /// foreach (var account in accounts)
        /// {
        ///     var addresses = await authSdk.ListAccountAddressesAsync(account.id);
        ///     foreach (var addr in addresses)
        ///     {
        ///         Debug.Log($"Chain: {addr.chainType}, Address: {addr.address}");
        ///     }
        /// }
        /// </code>
        /// </example>
        public UniTask<AccountAddressResponse[]> ListAccountAddressesAsync(string accountId)
        {
            return accountService.ListAccountAddressesAsync(accountId);
        }

        /// <summary>
        /// Updates the display name of an account.
        /// Aligned with updateAccountDisplayName() in peak-sdk-browser.
        /// </summary>
        /// <param name="accountId">The account ID to update.</param>
        /// <param name="displayName">The new display name for the account.</param>
        /// <returns>
        /// A task that resolves to the updated AccountResponse.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when accountId is null or empty.
        /// </exception>
        /// <exception cref="Peak.Exceptions.HttpException">
        /// Thrown when the request fails due to network errors or invalid session.
        /// </exception>
        /// <example>
        /// <code>
        /// var authSdk = sdk.Authenticate();
        ///
        /// var accounts = await authSdk.ListAccountsAsync();
        /// if (accounts.Length > 0)
        /// {
        ///     var updated = await authSdk.UpdateAccountDisplayNameAsync(accounts[0].id, "My Main Wallet");
        ///     Debug.Log($"Updated account name: {updated.displayName}");
        /// }
        /// </code>
        /// </example>
        public UniTask<AccountResponse> UpdateAccountDisplayNameAsync(string accountId, string displayName)
        {
            return accountService.UpdateAccountDisplayNameAsync(accountId, displayName);
        }

        /// <summary>
        /// Initialize private key import and get encryption bundle.
        /// This is the first step of the two-step private key import process.
        /// Aligned with initImportPrivateKey() in peak-sdk-node.
        /// </summary>
        /// <returns>
        /// A task that resolves to InitImportPrivateKeyResult containing the import bundle,
        /// organization ID, and user ID needed to encrypt the private key client-side.
        /// </returns>
        /// <exception cref="Peak.Exceptions.HttpException">
        /// Thrown when the request fails due to network issues or invalid session.
        /// </exception>
        /// <exception cref="Exception">
        /// Thrown when the Turnkey init import flow cannot be completed.
        /// </exception>
        /// <remarks>
        /// After calling this method, the client must:
        /// 1. Use TurnkeyUtils.EncryptPrivateKeyToBundle() to encrypt the private key
        /// 2. Call CompleteImportPrivateKeyAsync() with the encrypted bundle
        ///
        /// The import bundle is used to securely encrypt the private key before sending
        /// it to Turnkey's infrastructure.
        /// </remarks>
        /// <example>
        /// <code>
        /// var authSdk = sdk.Authenticate();
        ///
        /// // Step 1: Initialize import
        /// var initResult = await authSdk.InitImportPrivateKeyAsync();
        ///
        /// // Step 2: Encrypt private key client-side
        /// var encryptedBundle = TurnkeyUtils.EncryptPrivateKeyToBundle(new TurnkeyUtils.EncryptPrivateKeyToBundleParams
        /// {
        ///     privateKey = "0x1234...",
        ///     keyFormat = "HEXADECIMAL",  // "HEXADECIMAL" for EVM, "SOLANA" for Solana
        ///     importBundle = initResult.importBundle,
        ///     userId = initResult.userId,
        ///     organizationId = initResult.organizationId
        /// });
        ///
        /// // Step 3: Complete import
        /// var result = await authSdk.CompleteImportPrivateKeyAsync(encryptedBundle, "evm");
        /// Debug.Log($"Imported address: {result.accountAddress.address}");
        /// </code>
        /// </example>
        /// <seealso cref="CompleteImportPrivateKeyAsync(string, string)"/>
        /// <seealso cref="ExportPrivateKeyAsync(string, string)"/>
        public UniTask<InitImportPrivateKeyResult> InitImportPrivateKeyAsync()
        {
            return privateKeyService.InitImportPrivateKeyAsync();
        }

        /// <summary>
        /// Complete private key import using encrypted bundle.
        /// This is the second step of the two-step private key import process.
        /// Aligned with completeImportPrivateKey() in peak-sdk-node.
        /// </summary>
        /// <param name="encryptedBundle">
        /// The encrypted private key bundle from TurnkeyUtils.EncryptPrivateKeyToBundle().
        /// </param>
        /// <param name="chainType">
        /// The chain type for the imported key. Supported values: "evm", "solana".
        /// </param>
        /// <returns>
        /// A task that resolves to CompleteImportPrivateKeyResult containing the
        /// created account, account address, and account source information.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when encryptedBundle or chainType is empty, or chainType is not supported.
        /// </exception>
        /// <exception cref="Peak.Exceptions.HttpException">
        /// Thrown when the request fails due to network issues or invalid session.
        /// </exception>
        /// <exception cref="Exception">
        /// Thrown when the Turnkey complete import flow cannot be completed.
        /// </exception>
        /// <remarks>
        /// Before calling this method, you must:
        /// 1. Call InitImportPrivateKeyAsync() to get the import bundle
        /// 2. Use TurnkeyUtils.EncryptPrivateKeyToBundle() to encrypt the private key
        ///
        /// Key format per chain type:
        /// - "evm": Use keyFormat = "HEXADECIMAL" (32-byte hex with or without 0x prefix)
        /// - "solana": Use keyFormat = "SOLANA" (base58-encoded 64-byte key)
        /// </remarks>
        /// <example>
        /// <code>
        /// var authSdk = sdk.Authenticate();
        ///
        /// // Step 1: Initialize import
        /// var initResult = await authSdk.InitImportPrivateKeyAsync();
        ///
        /// // Step 2: Encrypt private key client-side
        /// var encryptedBundle = TurnkeyUtils.EncryptPrivateKeyToBundle(new TurnkeyUtils.EncryptPrivateKeyToBundleParams
        /// {
        ///     privateKey = "0x1234...",
        ///     keyFormat = "HEXADECIMAL",
        ///     importBundle = initResult.importBundle,
        ///     userId = initResult.userId,
        ///     organizationId = initResult.organizationId
        /// });
        ///
        /// // Step 3: Complete import
        /// var result = await authSdk.CompleteImportPrivateKeyAsync(encryptedBundle, "evm");
        /// Debug.Log($"Account ID: {result.account.id}");
        /// Debug.Log($"Address: {result.accountAddress.address}");
        /// </code>
        /// </example>
        /// <seealso cref="InitImportPrivateKeyAsync"/>
        /// <seealso cref="ExportPrivateKeyAsync(string, string)"/>
        public UniTask<CompleteImportPrivateKeyResult> CompleteImportPrivateKeyAsync(string encryptedBundle, string chainType)
        {
            return privateKeyService.CompleteImportPrivateKeyAsync(encryptedBundle, chainType);
        }

        /// <summary>
        /// Exports the encrypted private key bundle for a specific address.
        /// The client must decrypt the bundle using TurnkeyUtils.DecryptExportBundle().
        /// Supports both private-key and recovery-phrase backed addresses.
        /// </summary>
        /// <param name="address">
        /// The wallet address to export the private key for.
        /// </param>
        /// <param name="targetPublicKey">
        /// The P256 public key (uncompressed) to encrypt the export bundle with.
        /// Generate using TurnkeyUtils.GenerateP256KeyPair().
        /// </param>
        /// <returns>
        /// A task that resolves to an ExportPrivateKeyResult containing the encrypted
        /// export bundle and organization ID needed for decryption.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when the address or target public key is empty.
        /// </exception>
        /// <exception cref="Peak.Exceptions.HttpException">
        /// Thrown when the export request fails due to network issues or invalid session.
        /// </exception>
        /// <exception cref="Exception">
        /// Thrown when the export fails or the account source type is unsupported.
        /// </exception>
        /// <remarks>
        /// This method retrieves an encrypted private key bundle for backup or migration.
        /// The client is responsible for decrypting the bundle using the corresponding
        /// private key from the P256 keypair.
        ///
        /// Only addresses owned by the authenticated user can be exported.
        /// Internally uses ACTIVITY_TYPE_EXPORT_PRIVATE_KEY for private-key sources
        /// and ACTIVITY_TYPE_EXPORT_WALLET_ACCOUNT for recovery-phrase sources.
        /// </remarks>
        /// <example>
        /// <code>
        /// var authSdk = sdk.Authenticate();
        ///
        /// // Generate encryption keypair
        /// var keyPair = TurnkeyUtils.GenerateP256KeyPair();
        ///
        /// // Export encrypted private key bundle
        /// var result = await authSdk.ExportPrivateKeyAsync("0x742d35Cc...", keyPair.publicKeyUncompressed);
        ///
        /// // Decrypt client-side
        /// var privateKey = TurnkeyUtils.DecryptExportBundle(new TurnkeyUtils.DecryptExportBundleParams
        /// {
        ///     exportBundle = result.exportBundle,
        ///     embeddedKey = keyPair.privateKey,
        ///     organizationId = result.organizationId,
        ///     returnMnemonic = false,
        ///     keyFormat = "HEXADECIMAL"
        /// });
        /// </code>
        /// </example>
        /// <seealso cref="InitImportPrivateKeyAsync"/>
        /// <seealso cref="CompleteImportPrivateKeyAsync(string, string)"/>
        public UniTask<ExportPrivateKeyResult> ExportPrivateKeyAsync(string address, string targetPublicKey)
        {
            return privateKeyService.ExportPrivateKeyAsync(address, targetPublicKey);
        }
    }
}
