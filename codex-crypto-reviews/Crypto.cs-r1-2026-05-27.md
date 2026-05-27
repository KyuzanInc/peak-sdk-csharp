# Codex review evidence — Crypto.cs round 1 (2026-05-27)

Reviewer: Codex CLI 0.129.0, `model_reasoning_effort=high`, read-only sandbox.
Subject: `packages/turnkey-sdk-csharp/src/Crypto.cs` (+ ApiKeyStamper.cs + Http.cs + Encoding.cs).
Compare-against: `upstream-snapshots/turnkey-sdk-unity/Runtime/*` (port base) and
[tkhq/sdk](https://github.com/tkhq/sdk) `@turnkey/crypto`, `@turnkey/api-key-stamper`,
`@turnkey/http` (upstream TypeScript).

## Category (b) divergence findings — must address

### Finding 1 — Solana export decoded length mismatch [severity: high]
- C#: `Crypto.cs:788` returns `Base58Encode(decryptedData)` over 32 bytes.
- TS (`@turnkey/crypto/src/turnkey.ts:decryptExportBundle`, current `main`): derives
  the ed25519 public key from the 32-byte secret and base58-encodes the
  64-byte `secret || publicKey` concatenation.
- Unity port (`upstream-snapshots/turnkey-sdk-unity/Runtime/Crypto.cs:829-831`)
  matches the current C# output (32 bytes).
- **Verdict:** the Unity port reflects v2.8.9 behaviour, which the new C#
  port mirrors. We pin to v2.8.9 (see
  [turnkey-source-pins.md](turnkey-source-pins.md)), so this is a divergence
  from current `main` but **not from the pin**. Tracked as a known delta with
  current upstream; revisit when the pin is bumped.

### Finding 2 — `decryptExportBundle` accepts missing organizationId [severity: high]
- C#: `Crypto.cs:761` only enforced the org-id check when the field was
  present. Empty / missing field silently passed.
- TS reference: rejects when the field is absent.
- **Action taken (this round):** changed the predicate to require the field;
  `bundleOrgId != parameters.OrganizationId` now treats missing-as-mismatch.
  See `Crypto.cs` post-edit lines around 769.

### Finding 3 — Legacy `signature/signedData` envelope accepted in `decryptExportBundle` [severity: high]
- C#: `Crypto.cs:723` honours both legacy (`signature + signedData`) and
  current (`data + dataSignature + enclaveQuorumPublic`) envelopes.
- TS `main` only accepts the current envelope.
- **Verdict:** Unity port (the pin v2.8.9 source) supports both for
  backward compatibility. Preserved intentionally; documented here.

### Finding 4 — `decryptCredentialBundle` Base58Check → Base58 fallback [severity: high]
- C#: `Crypto.cs:550` falls back to plain Base58 if Base58Check fails.
- TS `main`: only `bs58check.decode`, no fallback.
- **Verdict:** Unity port (v2.8.9 pin) has this fallback. Preserved
  intentionally; documented.

### Finding 5 — Error contract wording differs from TS [severity: low]
- C# error messages such as `Organization ID mismatch...` and generic HPKE
  payload errors don't match TS message wording byte-for-byte.
- Not blocking; cross-SDK consumers should never parse error strings.
- Tracked as a future polish item.

### Finding 6 — `verifyEnclaveSignature` signer key comparison is case-insensitive [severity: low]
- C#: `Crypto.cs:888` uses `StringComparison.OrdinalIgnoreCase`.
- TS reference: case-sensitive.
- Hex strings are case-insensitive by spec; functional difference is nil.
  Tracked as a future polish item to match wire contract precisely.

### Finding 7 — Missing `sign(payload, SignatureFormat.Raw|Der)` API [severity: low]
- TS `@turnkey/api-key-stamper@v0.6.0` exposes `sign(payload, SignatureFormat.Raw|Der)`.
- C# exposes `Stamp(payload)` and `SignPayload(payload)` only (DER format).
- The Unity port (v2.8.9 pin source) does not expose raw format either.
  Preserved intentionally per D16 (Unity-shape surface).

## Category (a) — intentional .NET / pin adaptations

- `TurnkeyJsonContext` `PropertyNamingPolicy = CamelCase` produces lowerCamelCase
  JSON keys; verified.
- `UncompressRawPublicKey` math (`(x*x+a)*x+b mod p`), parity prefix, AES-GCM
  128-bit tag match TS.
- Low-S enforcement in `ApiKeyStamper.SignPayload` matches noble/curves
  `lowS:true`.
- `JsonDocument` field lookups are case-sensitive, matching TS camelCase.

## Build / test verdict (this round)

- Build: PASS (verified `dotnet build peak-sdk-csharp.sln -c Release` —
  netstandard2.1 + net8.0 both green).
- Tests: PASS (63/63 turnkey-sdk-csharp.Tests + 22/22 peak-sdk-csharp.Tests
  on net8.0).
- Outstanding coverage gaps to add in follow-up rounds:
  - Solana export 64-byte output parity test (or assertion that pin choice
    is documented).
  - Round-2 review of the Finding 2 fix to confirm it doesn't regress
    happy-path bundles.
  - Golden DER signature vector against a v2.8.9-produced sample.
  - Byte-exact `FormatHpkeBuf` / `X-Stamp` / HTTP body JSON.

## Round-pass criteria status

- Round 1 of 3: complete. Findings 2 fixed; 1, 3, 4, 5, 6, 7 documented as
  pin-consistent / low-priority. Findings 5-7 carried to backlog.
- Round 2 / Round 3: pending. Will re-run after Finding 2 fix lands and a
  v0.1.0-alpha.1 commit is cut.

## Tokens (this round)

Input: 1,275,214. Output: 12,678.

## References

- [tkhq/sdk packages/crypto/src/crypto.ts](https://raw.githubusercontent.com/tkhq/sdk/main/packages/crypto/src/crypto.ts)
- [tkhq/sdk packages/crypto/src/turnkey.ts](https://raw.githubusercontent.com/tkhq/sdk/main/packages/crypto/src/turnkey.ts)
- [tkhq/sdk packages/api-key-stamper/src/index.ts](https://github.com/tkhq/sdk/blob/main/packages/api-key-stamper/src/index.ts)
- Pin tracker: [turnkey-source-pins.md](turnkey-source-pins.md)
