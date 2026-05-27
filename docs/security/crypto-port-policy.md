# Crypto port policy

This document is normative for any change to
`packages/turnkey-sdk-csharp/src/Crypto.cs`,
`packages/turnkey-sdk-csharp/src/ApiKeyStamper.cs`,
`packages/turnkey-sdk-csharp/src/Http.cs`, or
`packages/turnkey-sdk-csharp/src/Encoding.cs`. These four files are
the cryptographic boundary of the SDK.

## What this SDK is and isn't

- It IS a 1:1 logical port of Turnkey's official open-source
  TypeScript SDK (`@turnkey/crypto`, `@turnkey/api-key-stamper`,
  `@turnkey/http`) at the pinned versions in
  [codex-crypto-reviews/turnkey-source-pins.md](../../codex-crypto-reviews/turnkey-source-pins.md).
  Each algorithm step, constant, and error path matches the TypeScript
  reference; differences are mechanical (`Task<T>` vs `Promise<T>`,
  `byte[]` vs `Uint8Array`, `BigInteger` vs `bigint`, etc.) and are
  documented inline.
- It IS multi-round Codex-reviewed against the TypeScript upstream
  before every merge to `main`. Evidence files live in
  `codex-crypto-reviews/`.
- It IS NOT paid-audited. A paid third-party crypto audit is a v1.0.0
  release blocker (open question OQ-N2). Until that completes, the
  SDK ships with the Unofficial Turnkey SDK disclaimer in
  [LICENSE](../../LICENSE) and in each package's `<Description>`.
- It IS NOT a fork of the TypeScript SDK. Upstream bug fixes do not
  flow in automatically; the resync workflow (see below) is operator-
  driven.

## Codex review is review evidence, not audit substitute

D17-CLAR in [plans/plans-peak-sdk-csharp.md](../../plans/plans-peak-sdk-csharp.md)
spells this out:

- Codex multi-round (≥ 3 rounds per crypto file) is a defence in
  depth. It catches logic divergence (different constant, different
  algorithm step, wrong error contract) with high recall when the
  prompt is correctly scoped.
- It is **not** an audit. It does not catch timing / side-channel
  attacks, RNG quality issues, ephemeral key reuse, invalid-curve /
  small-subgroup attacks, ECDSA low-S / signature malleability, DER
  edge cases, JSON canonicalisation drift, base64 / base58 leniency,
  AOT / IL2CPP trimming differences, or silent exception swallow that
  bypasses security policy.
- Coverage for those classes lives in
  `packages/turnkey-sdk-csharp/tests/CryptoTests.cs` (known-vector +
  property + fuzz) and, ultimately, in the v1.0.0 paid audit.

## Mandatory pre-merge checks for crypto-touching PRs

Before a crypto-touching PR can merge:

1. **Logic-equivalent baseline.** `git diff` against `main` shows the
   exact change; the PR description names the TypeScript upstream
   file(s) and lines that motivated the change.
2. **Codex multi-round review.** Run
   `codex-crypto-reviews/codex-crypto-review.sh <file.cs>` three times
   in a row. The latest three evidence files MUST report "No logic
   divergence found." Older evidence is preserved for history.
3. **Test coverage.** The test fixture matrix in
   `packages/turnkey-sdk-csharp/tests/Fixtures/README.md` is full.
   Every algorithm path has at least one positive vector, one
   negative case, and (for HPKE) a fixed-ephemeral byte-exact fixture
   so encryption drift is detectable.
4. **CI matrix.** Build + test PASS on Ubuntu, Windows, and macOS,
   for both `netstandard2.1` and `net8.0` targets.
5. **Security label.** PRs touching these four files carry the
   `crypto` GitHub label; CODEOWNERS auto-requests Komy CTO + TJ.

## Forbidden in crypto files

- `Newtonsoft.Json` — must use `System.Text.Json` with source-generated
  contexts so AOT / IL2CPP / trimming is safe.
- `using UnityEngine` — Unity APIs are forbidden in the generic core
  package. Unity-specific code lives only in
  `packages/peak-sdk-csharp-unity/`.
- `Random` / `System.Random` for any key material — use
  `Org.BouncyCastle.Security.SecureRandom` or
  `System.Security.Cryptography.RandomNumberGenerator`.
- `MD5`, `SHA1`, `DES`, `RC4`, `3DES` for any signature, MAC, or KDF.
- Hard-coded production secrets. The notarizer and signer public keys
  in `Crypto.cs` are public constants; they identify Turnkey, they do
  not authenticate anything else.

## Upstream resync workflow

When `@turnkey/*` ships a relevant new version:

1. Re-pin in [codex-crypto-reviews/turnkey-source-pins.md](../../codex-crypto-reviews/turnkey-source-pins.md).
2. Re-snapshot the TypeScript source via
   `scripts/sync-upstream.sh tkhq-sdk <new-version>`. This produces a
   single commit on the resync branch.
3. Open a `port:` prefixed PR that applies the equivalent C# changes.
4. Run a fresh Codex multi-round review (do not reuse prior evidence).
5. Add new known-vector fixtures from the upstream test suite if the
   change touches an algorithm path with fixed vectors (HPKE, HKDF,
   ECDSA).
6. Bump the SDK SemVer per the public-API impact.

## Reporting a suspected bug

If you suspect a crypto bug in this SDK, do not file a public issue.
Email <security@kyuzan.com> with reproduction and severity
estimate. The maintainers will respond within 5 business days.
