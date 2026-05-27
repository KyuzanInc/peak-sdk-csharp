# Codex multi-round crypto review

This directory holds the **review evidence** for every file in
`packages/turnkey-sdk-csharp/src/` whose logic comes from the official
Turnkey TypeScript SDK. Per `plans/plans-peak-sdk-csharp.md` D17-CLAR,
Codex review is review evidence, not a paid security audit. A paid
audit is still required before v1.0.0.

## How to run a review round

```
codex-crypto-reviews/codex-crypto-review.sh <file.cs> [round_number]
```

The script:

1. Reads the C# file at `packages/turnkey-sdk-csharp/src/<file.cs>`.
2. Reads the corresponding TypeScript source at
   `upstream-snapshots/turnkey-official-src/<package>/src/<file.ts>`
   (resolved via `turnkey-source-pins.md`).
3. Invokes `codex exec` with the review prompt template (see below).
4. Saves the response to
   `codex-crypto-reviews/<file.cs>-r<N>-<YYYY-MM-DD>.md`.

Pass criteria for a file: **three consecutive rounds** that report
"no logic divergence found" in the actual-logic-divergence category
(category b in the prompt). Cosmetic .NET adaptations
(`Task<T>` vs `Promise`, `byte[]` vs `Uint8Array`, etc.) are category
a and are expected.

## Review prompt template

```
You are reviewing a C# port of Turnkey official source code
(@turnkey/<package>@<version>). The pinned official commit SHA is in
turnkey-source-pins.md. Compare the attached C# file with the official
TypeScript source on github.com/tkhq/sdk at that commit. Identify every
place where the C# logic deviates from the TypeScript source (different
algorithm step, different constant, different error handling, different
rounding/normalization).

Distinguish between:
(a) intentional .NET adaptation (Task<T> vs Promise, byte[] vs
    Uint8Array, BigInteger vs bigint, etc.) which is OK, and
(b) actual logic divergence which is NOT OK.

List the second category only. For each entry give:
- C# file + line number
- TypeScript file + line number
- One-paragraph description of the divergence
- Severity: critical (changes wire format or security property),
  high (changes error surface but not security), low (changes
  performance / readability only)
```

## File map

| C# file | TS source | Codex review round target |
|---|---|---|
| `packages/turnkey-sdk-csharp/src/Crypto.cs` | `@turnkey/crypto/src/*.ts` (multiple) | ≥ 3 rounds |
| `packages/turnkey-sdk-csharp/src/ApiKeyStamper.cs` | `@turnkey/api-key-stamper/src/index.ts` | ≥ 3 rounds |
| `packages/turnkey-sdk-csharp/src/Http.cs` | `@turnkey/http/src/*.ts` (stamping subset) | ≥ 3 rounds |

`Encoding.cs` is a port of `@turnkey/encoding` and is reviewed in
round 1 of `Crypto.cs` (its consumers).

## What Codex review will NOT catch

Per the C-C1 finding (see `plans/plans-peak-sdk-csharp.md` Codex
adjudication table), Codex multi-round review is static reasoning. It
will NOT systematically catch:

- Timing / side-channel leaks
- RNG quality / ephemeral key reuse
- Invalid-curve / small-subgroup attacks
- ECDSA low-S / signature malleability
- DER encoding edge cases (high-bit r/s, leading zero, etc.)
- JSON canonicalisation mismatch with peer implementations
- Base64 / Base58 leniency vs strictness
- AOT / IL2CPP trimming differences
- Silent exception swallow that bypasses security policy

Coverage for these classes lives in:

- `packages/turnkey-sdk-csharp/tests/CryptoTests.cs` (vector + fuzz +
  property tests)
- A future paid audit (v1.0.0 blocker, OQ-N2)
