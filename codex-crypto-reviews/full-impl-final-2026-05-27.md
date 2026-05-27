# Codex review evidence — full implementation final (2026-05-27)

Reviewer: Codex CLI 0.129.0, `model_reasoning_effort=high`, read-only sandbox.
Subject: full implementation at commit `6847839`. Covers PR 2 (peak-sdk-csharp
PeakClient + services + IStorage), PR 4.5 (DpapiSecureStorage +
UnavailableSecureStorage), and the publish workflow.

## P1 findings — all fixed inline

### 1. CompleteOtpLogin doesn't clear prior session before save [high]
- C# `PeakClient.cs:90` set the new session without first deleting the old
  one. Unity port (`PeakSdk.cs:284-285`) calls `SessionStorage.Clear()` then
  `SessionStorage.Save(...)`.
- **Fix:** added `storage.Delete(SessionData.StorageKey)` before the
  `Set` call. Matches Unity behaviour; protects against stale-after-partial-
  write on persistent backends.

### 2. `net8.0-windows` build fails on non-Windows CI [high]
- `peak-sdk-csharp.csproj` had `<TargetFrameworks>netstandard2.1;net8.0;net8.0-windows</TargetFrameworks>`
  but no `EnableWindowsTargeting`. NuGet refuses to restore on macOS / Linux
  without that flag.
- **Fix:** added `<EnableWindowsTargeting>true</EnableWindowsTargeting>`.
  The build now produces `bin/Release/net8.0-windows/` on macOS as well.

### 3. `Content-Type` header diverges from Unity [high]
- C# `DefaultPeakHttpClient.cs:69` was passing `new StringContent(json, Encoding.UTF8, "application/json")`,
  which .NET expands to `application/json; charset=utf-8`. Unity port sends
  bare `application/json`.
- **Fix:** switched to `ByteArrayContent` with an explicit
  `MediaTypeHeaderValue("application/json")`. Wire value is now exactly
  `application/json` (no `charset` suffix).

### 4. DpapiSecureStorage namespace allows path traversal [low → fixed]
- `DpapiSecureStorage` constructor concatenated `@namespace` via
  `Path.Combine` without validation. A hostile namespace like
  `../../../etc/passwd` could escape the LOCALAPPDATA root.
- **Fix:** namespace is now validated against `[A-Za-z0-9._-]+`; any
  other character throws `ArgumentException` at construction time.

## Findings accepted as known divergences

### 5. JWT validation errors surface as `SDK_INVALID_JWT`, not `SDK_AUTHENTICATION_FAILED` [low]
- Unity port wraps any JWT validation failure as
  `SdkException(AUTHENTICATION_FAILED)`. C# surfaces `PeakError(SDK_INVALID_JWT)`
  for non-expired-but-otherwise-invalid JWTs.
- **Verdict:** intentional. The TS family error model uses
  `PeakError(code='SDK_INVALID_JWT')` for the same case (see
  `plans/plans-peak-sdk-csharp.md` D7 + D21). The C# port aligns with TS
  rather than Unity here; the change is documented in
  `docs/security/crypto-port-policy.md`.

### 6. `PeakJsonContext` drops null fields [low]
- `DefaultIgnoreCondition = WhenWritingNull` excludes null properties from
  the wire JSON. Unity `JsonUtility.ToJson` emits all fields.
- **Verdict:** accepted. peak-server tolerates missing optional fields
  (per the OpenAPI spec). Happy-path required fields are always set; null
  values appearing on the wire would carry no information.

## Build / test verdict (post-fix)

```
dotnet build peak-sdk-csharp.sln -c Release
  → PASS (netstandard2.1 + net8.0 + net8.0-windows on macOS host)
dotnet test peak-sdk-csharp.sln -c Release --filter "Category!=E2E"
  → 22/22 PASS (peak-sdk-csharp) + 63/63 PASS (turnkey-sdk-csharp) = 85/85
```

## Recommendation

Ready to merge for v0.1.0-alpha.1. All three P1 findings are resolved by the
fix commit; the remaining two findings are accepted divergences documented
in this evidence file and in the storage threat-model.

## Tokens

Input: 1,137,553. Output: 9,749.
