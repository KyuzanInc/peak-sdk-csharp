# KyuzanInc.Turnkey.Sdk

A community-maintained .NET port of Turnkey's open-source TypeScript
SDK. Provides P-256 ECDSA, HPKE, HKDF, and API key stamping primitives
for any modern .NET host (Unity, Godot, .NET 8 console, ASP.NET,
.NET MAUI).

## What this is

- A 1:1 logical port of `@turnkey/crypto@2.8.9`,
  `@turnkey/api-key-stamper@0.6.0`, and `@turnkey/http@3.16.0`. Each
  algorithm step, constant, and error path mirrors the TypeScript
  upstream.
- Built on top of [`BouncyCastle.Cryptography`](https://www.nuget.org/packages/BouncyCastle.Cryptography)
  for the primitive backend.
- AOT- and IL2CPP-safe: `System.Text.Json` source generation is used
  for every serialisation path, so reflection-based JSON code never
  runs.
- Dual-targeted: `netstandard2.1` (Unity 2021.2+ compatible) plus
  `net8.0` for native modern .NET performance.

## What this is NOT

This SDK is unofficial. It is published by Kyuzan Inc. and is not
affiliated with Turnkey, Inc.

The crypto port has been reviewed by multi-round automated code
review (Codex) but has not undergone a paid third-party security
audit. A paid audit is planned for the v1.0.0 release. Use at your
own risk for anything beyond pre-production work.

## Install

```
dotnet add package KyuzanInc.Turnkey.Sdk
```

## Quick start

```csharp
using KyuzanInc.Turnkey.Sdk;

// Sign a payload with a Turnkey API key
var stamper = new ApiKeyStamper(apiPublicKeyHex, apiPrivateKeyHex);
var stamp = stamper.Stamp("the request body JSON");
// stamp.StampHeaderName == "X-Stamp"
// stamp.StampHeaderValue is the base64url-encoded JSON envelope

// Build a signed Turnkey activity
var http = Http.FromTargetPrivateKey(targetPrivateKeyHex);
var whoami = http.StampGetWhoami(organizationId: "org-123");
// whoami.Url, whoami.Body, whoami.Stamp are ready to ship via your
// own HttpClient or IHttpClientFactory of choice.
```

## Surface

- `Encoding` — hex, Base58, Base58Check, byte-array helpers.
- `Crypto` — HPKE encrypt/decrypt, ECDH, key generation, HKDF,
  Tonelli-Shanks modular square root, credential bundle parse /
  build, session-JWT signature verification.
- `ApiKeyStamper` — P-256 ECDSA stamping with deterministic k
  (RFC 6979), DER signature output, low-S enforcement (BIP-62),
  base64url stamp envelope.
- `Http` — typed activity stampers: `StampGetWhoami`,
  `StampInitImportPrivateKey`, `StampImportPrivateKey`,
  `StampExportPrivateKey`, `StampExportWalletAccount`.

## License

[MIT](https://github.com/KyuzanInc/peak-sdk-csharp/blob/main/LICENSE).
Crypto primitives are ported from Turnkey's open-source TypeScript
SDK under its license terms; see the upstream snapshot in
[upstream-snapshots/turnkey-official-src/](https://github.com/KyuzanInc/peak-sdk-csharp/tree/main/upstream-snapshots/turnkey-official-src)
for the original notices.

## Reporting a security issue

Do not open a public issue. Email <security@kyuzan.com> with details
and severity estimate. The maintainers will respond within 5
business days.
