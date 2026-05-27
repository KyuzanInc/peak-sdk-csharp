# Claude Code Instructions — peak-sdk-csharp

**Purpose**: Safety guardrails for AI assistants working in this repo.

**Writing style**: C1-level English or simpler for all docs and code comments.

> For project overview see [README.md](README.md).
> For the active port plan see [plans/plans-peak-sdk-csharp.md](plans/plans-peak-sdk-csharp.md).
> For architecture see [docs/architecture.md](docs/architecture.md).
> For security policy see [docs/security/storage-threat-model.md](docs/security/storage-threat-model.md)
> and [docs/security/crypto-port-policy.md](docs/security/crypto-port-policy.md).

---

## What this repo is

A .NET NuGet port of the Unity-only `peak-sdk-unity` + `turnkey-sdk-unity`
SDKs. The cryptographic core is a 1:1 logical port of
`@turnkey/{crypto,api-key-stamper,http}` (pinned versions in
[upstream-snapshots/SOURCES.md](upstream-snapshots/SOURCES.md)).

## What this repo is NOT

- It is NOT the canonical source of the Peak public OpenAPI spec. That
  lives in the `KyuzanInc/peak` monorepo. We sync the spec into
  `upstream-snapshots/peak-server-openapi/` at fixed tags.
- It is NOT a fork of the official Turnkey SDK. We are a separate,
  unofficial community port. See [LICENSE](LICENSE) for the disclaimer.
- It is NOT yet audited. The crypto port has been reviewed by multi-round
  Codex review (see [codex-crypto-reviews/](codex-crypto-reviews/)); a
  paid third-party audit is still scheduled for the v1.0.0 milestone.

---

## Cryptographic code (`packages/turnkey-sdk-csharp/`)

Crypto code is security-critical. Before editing **any** file under
`packages/turnkey-sdk-csharp/src/Crypto.cs`, `ApiKeyStamper.cs`, or
`Http.cs`:

1. Read the corresponding file in `upstream-snapshots/turnkey-official-src/`
   (the pinned TypeScript source).
2. State the algorithm change in the PR description with line citations
   on both sides.
3. Run the Codex multi-round review script
   `codex-crypto-reviews/codex-crypto-review.sh <file>` and commit the
   resulting evidence file under `codex-crypto-reviews/`.
4. Update the per-file pin in `codex-crypto-reviews/turnkey-source-pins.md`
   if you intentionally moved to a newer upstream commit.

A change without those three artifacts WILL be reverted on review.

## Auto-generated code

These files are generated; never hand-edit them:

- `packages/peak-public-api-client-csharp/src/**/*` — OpenAPI codegen
- `upstream-snapshots/peak-server-openapi/public-api.yaml` — synced from
  `KyuzanInc/peak` at a pinned tag (see
  [docs/sync-rules.md](docs/sync-rules.md))
- `upstream-snapshots/turnkey-official-src/` — pinned upstream copy
- `upstream-snapshots/peak-sdk-unity/` and
  `upstream-snapshots/turnkey-sdk-unity/` — pinned port sources

To resync, follow the procedure in [docs/sync-rules.md](docs/sync-rules.md).

## Storage and secure-storage code (`packages/peak-sdk-csharp/src/Storage/`)

Wallet SDK security policy:

- `InMemoryStorage` is the only default. Anything persistent must be
  explicit opt-in.
- `UnsafePlaintextPlayerPrefsStorage` (in the Unity adapter) requires a
  compile symbol (`PEAK_UNSAFE_STORAGE_OPT_IN`) **and** an explicit
  per-instance `acknowledgePlaintext: true` argument.
- `DpapiSecureStorage` is Windows-only. Other platforms have no built-in
  secure storage in v0.1.0; `ISecureStorage.IsAvailable` returns `false`.
  Consumers MUST handle that case. The threat model
  ([docs/security/storage-threat-model.md](docs/security/storage-threat-model.md))
  is normative.

## Working with this repo

Do not introduce dependencies on:

- `UnityEngine` outside `packages/peak-sdk-csharp-unity/`
- `Newtonsoft.Json` anywhere (use `System.Text.Json` with source-generated
  contexts for AOT/IL2CPP safety)
- A new logger abstraction (use `Microsoft.Extensions.Logging.Abstractions`
  and `NullLogger<T>.Instance` as default)

Before committing crypto changes, verify:

```bash
dotnet test peak-sdk-csharp.sln --filter Category=Crypto
```

is green and produces full test fixtures (RFC 9180 §A.3 HPKE, RFC 5869
HKDF, NIST CAVP P-256, Turnkey sample bundles, JWT positive/negative).

## Branch and commit conventions

- `main` is the default branch (no `develop` separate from `main` here).
- Commit message prefix: `feat`, `fix`, `chore`, `docs`, `test`, `refactor`,
  `port` (for upstream port commits).
- For crypto-touching commits, mention the upstream commit SHA in the
  message body.
