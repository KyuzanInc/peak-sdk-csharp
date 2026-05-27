# Peak SDK Unity

Unity SDK for Peak's embedded wallet, supporting iOS and Android.

## Requirements

- Unity 6000.0.5f1 or later
- iOS 13.0+ / Android API 24+

## Installation

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.kyuzan.peak-sdk-unity": "https://github.com/KyuzanInc/peak-sdk-unity.git"
  }
}
```

## Quick Start

### 1. Configure Settings

1. Open **Peak SDK > Settings** in Unity Editor
2. Enter your Project API Key
3. Select your environment

### 2. Initialize and Authenticate

```csharp
using Peak;
using UnityEngine;

public class AuthExample : MonoBehaviour
{
    private PeakSdk sdk;

    private async void Start()
    {
        // Initialize SDK
        sdk = PeakSdk.Initialize();
    }

    public async void Login(string email, string otpCode, string otpId)
    {
        // Complete OTP login
        await sdk.CompleteOtpLoginAsync(email, otpId, otpCode);

        // Get authenticated SDK
        var authSdk = sdk.Authenticate();

        // Now you can use authenticated operations
        var accounts = await authSdk.ListAccountsAsync();
    }

    public async void StartLogin(string email)
    {
        // Send OTP to email
        var result = await sdk.InitOtpLoginAsync(email);
        Debug.Log($"OTP sent. ID: {result.otpId}");
    }

    public void Logout()
    {
        sdk.Logout();
    }
}
```

## API Overview

### PeakSdk (Unauthenticated)

| Method | Description |
|--------|-------------|
| `Initialize()` | Initialize SDK with Unity Editor settings |
| `Initialize(SdkOptions)` | Initialize SDK with custom options |
| `InitOtpLoginAsync(email)` | Start OTP login flow |
| `CompleteOtpLoginAsync(email, otpId, otpCode, signup)` | Complete OTP login (creates user if missing when signup=true, default) |
| `Authenticate()` | Get authenticated SDK instance |
| `Logout()` | Clear session data |

### AuthenticatedPeakSdk

| Method | Description |
|--------|-------------|
| `ListAccountsAsync()` | List user's wallet accounts |
| `ListAccountAddressesAsync(accountId)` | Get addresses for an account |
| `InitImportPrivateKeyAsync(...)` | Start private key import |
| `CompleteImportPrivateKeyAsync(...)` | Complete private key import |
| `ExportPrivateKeyAsync(...)` | Export private key |

## Session Management

Sessions are automatically persisted using Unity's PlayerPrefs. After login, players remain authenticated across app restarts until:

- `Logout()` is called
- Session JWT expires (project-configured, default 60 minutes)
- App is uninstalled

```csharp
// Check if player is already logged in
try
{
    var authSdk = sdk.Authenticate();
    // Player is logged in
}
catch (NotAuthenticatedException)
{
    // Player needs to login
}
catch (TokenExpiredException)
{
    // Session expired, player needs to login again
}
```

## Documentation

For detailed documentation, see the [Peak SDK Unity Documentation](https://docs.peak.xyz/sdks-and-tools/peak-sdk-unity).

## License

MIT License - see [LICENSE.md](LICENSE.md)
