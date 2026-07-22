# Sync rules — public OpenAPI contract and Turnkey SDK pin

`peak-sdk-csharp` retains one sanitized OpenAPI contract in
`upstream-snapshots/peak-server-openapi/`. Complete upstream repositories are
not mirrored here. Provenance is recorded as an exact commit plus checksums in
[`docs/compatibility/upstream-pins.md`](compatibility/upstream-pins.md).

The Turnkey crypto layer is consumed from the external
[`KyuzanInc.Turnkey.Sdk`](https://github.com/KyuzanInc/turnkey-sdk-csharp)
private package at the exact stable `[1.0.0]` dependency boundary.

## When to update

| Trigger | Affected input | Operator action |
|---|---|---|
| `KyuzanInc/peak` publishes a relevant public API change | `upstream-snapshots/peak-server-openapi/public-api.yaml` | import from `main`, sanitize, regenerate, and update provenance/checksum records |
| `KyuzanInc/turnkey-sdk-csharp` cuts a compatible stable release | `Directory.Packages.props` and lock files | review the release and explicitly decide whether to move the exact dependency pin |
| A consumer reports a behavior mismatch | hand-written DTO or request surface | investigate the C# surface; do not import a complete source tree |

## OpenAPI workflow

Run the importer only for the OpenAPI source:

```bash
./scripts/sync-upstream.sh peak-server-openapi main
```

The script clones `KyuzanInc/peak`, replaces only the working-tree OpenAPI
contract and `PIN.md`, and prints the resolved commit plus imported raw-contract
SHA-256. It does not create or switch a branch, stage files, commit, or otherwise
mutate the repository index. The copied contract is untrusted raw input until
the following sequence completes:

1. Record the exact resolved commit and imported raw-contract SHA-256 in
   `upstream-snapshots/SOURCES.md` and
   `docs/compatibility/upstream-pins.md`.
2. Apply all deterministic public-contract substitutions to
   `upstream-snapshots/peak-server-openapi/public-api.yaml`:
   - replace `servers` with one `https://api.example.invalid/` entry;
   - replace the `Stamp.stampHeaderValue` example with `synthetic-stamp`;
   - replace the `SignedRequest` `organizationId` with
     `00000000-0000-0000-0000-000000000000`;
   - replace every `9c7076e7-c8d4-4b7b-a484-e9ed28f3931d` example with
     `00000000-0000-0000-0000-000000000000`;
   - replace the Google callback URL with
     `https://wallet.example.invalid/auth/google/callback`; and
   - replace the Google OIDC token example with `synthetic.jwt.token`.
3. Run `scripts/generate-public-api-client.sh` (OpenAPI Generator 7.9.0).
4. Calculate the sanitized contract SHA-256. Write exactly
   `<lowercase-sha256>  upstream-snapshots/peak-server-openapi/public-api.yaml`
   as the sole line of
   `tests/UpstreamSources/peak-server-openapi-public-api.sha256`, and record the
   same public digest in `docs/compatibility/upstream-pins.md`.
5. Run `bash tools/publication/verify-public-tree.sh` and require PASS. Run the
   focused generated-client build/tests as applicable. Do not stage on failure.
6. Only after the gate passes, stage every sanitized/generated/provenance/
   checksum output:

   ```bash
   git add -- \
     upstream-snapshots/peak-server-openapi/public-api.yaml \
     upstream-snapshots/peak-server-openapi/PIN.md \
     upstream-snapshots/SOURCES.md \
     docs/compatibility/upstream-pins.md \
     tests/UpstreamSources/peak-server-openapi-public-api.sha256 \
     packages/peak-public-api-client-csharp/src \
     packages/peak-public-api-client-csharp/.openapi-generator
   ```

7. Inspect `git diff --cached --check` and the full `git diff --cached` before
   committing and opening the dedicated pull request. A checksum mismatch is a
   hard failure.

The only accepted name is `peak-server-openapi`; this repository has no Unity
snapshot synchronization path. Create any dedicated sync branch explicitly
before invoking the importer; the importer never changes repository HEAD or the
index.

## Turnkey dependency updates

`KyuzanInc.Turnkey.Sdk` is currently pinned to `[1.0.0]`. To propose a later
stable version:

1. Review its public source and release notes and confirm the private package
   exists at the proposed exact version.
2. Update `Directory.Packages.props` while preserving the square-bracket exact
   version syntax.
3. Re-resolve the source and test lock files with
   `dotnet restore peak-sdk-csharp.sln --force-evaluate`.
4. Inspect both lock-file diffs.
5. Run
   `dotnet test peak-sdk-csharp.sln --filter "FullyQualifiedName~TurnkeyWireFormatSmokeTests"`.
6. Review the change in a dedicated pull request.

## Generated client boundary

`KyuzanInc.Peak.Sdk` deserializes runtime responses into hand-written
System.Text.Json DTOs in `Models/Models.cs` through `PeakJsonContext`. The
generated `KyuzanInc.Peak.PublicApiClient` types are internal, build-time-only,
and excluded from the shipped SDK package. They exist for deterministic drift
and field-coverage checks.

The `openapi-client-drift` CI job regenerates from the committed sanitized
contract and compares the generated `src/` tree byte-for-byte. It needs Java
and Node 20.19.0 or later, but it does not fetch `KyuzanInc/peak`.
