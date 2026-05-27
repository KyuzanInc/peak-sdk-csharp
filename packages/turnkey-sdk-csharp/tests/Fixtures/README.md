# Test fixtures

Each fixture in this directory cites its origin. Fixtures are committed
text files; no network access during test runs.

## RFC 5869 (HKDF SHA-256)

The HKDF test cases in [CryptoTests.cs](../CryptoTests.cs) are
inlined from RFC 5869 §A.1 (Test Case 1), §A.2 (Test Case 2), and
§A.3 (Test Case 3) — the SHA-256 variants. Source:
<https://www.rfc-editor.org/rfc/rfc5869#appendix-A>.

## Bitcoin BIP-13 (Base58)

The Base58 known-vectors in [EncodingTests.cs](../EncodingTests.cs)
are taken from the Bitcoin reference implementation's test data
(`src/test/data/base58_encode_decode.json`). The set used here is a
deliberately small subset for v0.1.0; a larger fuzz pass is on the
backlog (Wc1 fuzz tests).

## Turnkey sample bundles (future)

Per the plan's PR 1 fixture matrix, we plan to land:

- `turnkey-credential-bundle-sample.json` — sample export bundle in
  the current envelope format
- `turnkey-export-bundle-legacy-sample.json` — same in the legacy
  signedData/signature envelope
- `turnkey-import-bundle-sample.json` — sample import bundle to
  round-trip through `EncryptPrivateKeyToBundle`
- `turnkey-issued-jwt-sample.txt` — sample session JWT signed by the
  production notarizer key

These require Turnkey sandbox access (OQ-N1 / OQ5 in the original
plan). Until they're committed, the test files cover structural
validation only; the cryptographic happy-path is covered by the HPKE
round-trip test and by the multi-round Codex review evidence under
[../../codex-crypto-reviews/](../../codex-crypto-reviews/).

## Fixed-ephemeral HPKE fixture (future)

Per Codex finding C-A2, the HpkeEncrypt round-trip test does not
catch ephemeral-key drift. The plan calls for a fixed-ephemeral hook
fixture: a sample where the ephemeral private key is deterministic
and the expected ciphertext bytes are committed. This requires either
exposing a test seam in `Crypto.cs` (an internal-visible-to-tests
constructor with an injected `SecureRandom`) or pre-computing the
fixture via the upstream `@turnkey/crypto` and committing it.

The seam approach is preferred. It will land in a follow-up commit
once the upstream-snapshot of `@turnkey/crypto` is on disk (see
[../../../docs/sync-rules.md](../../../docs/sync-rules.md)).
