# C# Public API Client Codegen (Option B) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up an internal C# OpenAPI client (`KyuzanInc.Peak.PublicApiClient`) that is generated from the pinned `peak-server` spec with the same engine peak uses, plus a drift-check CI job — so the C# client tracks the spec the same way peak's TypeScript client does.

**Architecture:** peak owns the spec (`KyuzanInc/peak` → `apps/peak-public-docs/docs/api-references/public-api.yaml`, generated from NestJS swagger). We mirror that spec read-only into `upstream-snapshots/peak-server-openapi/`, tracking `main` HEAD (recorded as an exact commit in `PIN.md`), then run the SAME engine (`@openapitools/openapi-generator-cli`, core pinned to `7.9.0` via `openapitools.json`) with the `csharp` generator instead of `typescript-axios`. The generated client is committed and drift-checked in CI. It is **not** wired into `KyuzanInc.Peak.Sdk` in this plan (that consumer rewrite is a separate follow-up); here it only has to generate, build, and stay in sync.

**Tech Stack:** .NET 8 SDK (TFMs `net8.0;netstandard2.1`), `@openapitools/openapi-generator-cli@2.22.0` (generator core `7.9.0`, run via `npx`, needs a JRE), RestSharp 112.0.0 / Polly 8.1.0 / Newtonsoft.Json 13.0.3 / System.ComponentModel.Annotations 5.0.0, Central Package Management, GitHub Actions.

> **Superseded pin (read first):** Task 1 and a few early sections below were written when the spec was pinned to tag `v0.3.0`. That is historical — the shipped behaviour tracks `KyuzanInc/peak` `main` HEAD (recorded as an exact commit in `upstream-snapshots/peak-server-openapi/PIN.md`). Resync with `scripts/sync-upstream.sh peak-server-openapi main`, not a tag. See "Implementation amendments (post-landing) → 3".

---

## Validated facts (reconnaissance already done — do not re-litigate)

Every artifact below was trial-run locally against the real spec before this plan was written:

- **Engine/version:** the `@openapitools/openapi-generator-cli@2.22.0` npm wrapper defaults to core **7.14.0**; it is pinned to **7.9.0** (peak's version) only when an `openapitools.json` with `generator-cli.version = 7.9.0` sits in the working directory. The wrapper auto-writes a `7.14.0` file if none exists, so the pinned file MUST be committed.
- **Generator library:** `restsharp` (matches the deps pre-staged in `Directory.Packages.props`). `useSystemTextJson` is **ignored** by the restsharp library in 7.9.0 — the models use **Newtonsoft.Json** (66 refs). `JsonSubTypes` is referenced by the generated `.csproj` but **0 times in code** → safe to omit.
- **Generated deps that matter:** RestSharp, Polly (v7 API surface: `Policy<RestResponse>` — compiles on Polly 7 and 8), Newtonsoft.Json, and — only on `netstandard2.1` — `System.ComponentModel.Annotations` (the models use `System.ComponentModel.DataAnnotations`; net8.0 has it built in, netstandard2.1 does not).
- **RestSharp version:** the generator's own pin **112.0.0** is used because **111.4.1 carries GHSA-4rr6-2v9v-wcpc** (NU1902, medium). 112.0.0 + `NoWarn=CS0618` builds with **0 warnings**.
- **`System.Web`:** the generated `Client/ApiClient.cs` has an unconditional `using System.Web;` and the generated `.csproj` adds `<Reference Include="System.Web" />`. The reference fails to resolve on .NET 8 (MSB3245/MSB3243). Dropping the reference is safe — the `using` is unused and compiles clean without it.
- **Root strictness:** `Directory.Build.props` sets `TreatWarningsAsErrors=true` and `Nullable=enable` for every csproj. Generated code violates both, so the client csproj must override `TreatWarningsAsErrors=false` and `Nullable=annotations` and carry a `NoWarn`.
- **Final ship combo verified:** a hand-authored csproj referencing `RestSharp` / `Polly` / `Newtonsoft.Json` / `System.ComponentModel.Annotations` (version-less, under root CPM), with `TreatWarningsAsErrors=false` and `NoWarn=$(NoWarn);CS1591;CS8019;CS0618`, compiling only `src/KyuzanInc.Peak.PublicApiClient/**/*.cs`, builds **both** TFMs with **0 warnings / 0 errors**.
- **Custom `.openapi-generator-ignore` verified:** the denylist below leaves only `src/KyuzanInc.Peak.PublicApiClient/{Api,Client,Model}/*.cs` plus `.openapi-generator/{FILES,VERSION}` — no `.csproj`, `.sln`, `docs/`, `api/`, `appveyor.yml`, `git_push.sh`, `README.md`, `.gitignore`, or sample `.Test/` project.
- **Spec pin:** `v0.3.0` (latest peak tag) contains `public-api.yaml` (1480 lines) with the init/complete OTP-login, accounts, and private-keys endpoints. This is the provisional pin pending OQ-N1 confirmation by Komy CTO.
- **`scripts/sync-upstream.sh` bug:** the `peak-server-openapi` branch copies into `$DEST` without `mkdir -p "$DEST"` (only the `peak-sdk-unity` branch creates the dir), so the first sync fails. Fixed in Task 1.

## Scope

**In scope:** spec snapshot import, generator config + pinning, generation script, the generated client committed + building in the solution, a drift-check CI job, the `sync-upstream.sh` fix, and doc/status alignment.

**Out of scope (explicit):** wiring the generated client into `KyuzanInc.Peak.Sdk` (replacing its hand-written HTTP layer), publishing the client as a NuGet package, and the cross-repo "peak merge auto-triggers C# regen" dispatch (the optional B+ automation). The client is internal (`IsPackable=false`), referenced by nothing yet, and exists to prove the pipeline + guard drift.

## Follow-ups (tracked, not in this PR)

These are named so they are not silently lost. They are deliberately deferred, not forgotten:

1. **Consumer wiring (the thing that makes this client earn its keep).** Land in PR 2: replace `KyuzanInc.Peak.Sdk`'s hand-written HTTP/DTOs with the generated client (or map generated Newtonsoft DTOs → the public System.Text.Json surface). **Why deferred:** it couples this infra change to a DTO-wrapper rewrite and to PR 2's auth surface, and OQ-N1 is still open. **Watch:** a generated client that no PR consumes is a drift-CI maintenance tax with no payoff — if PR 2 slips materially, revisit whether to keep this landed or revert.
2. **B+ automatic trigger (true parity with peak).** A `repository_dispatch` from peak on a `public-api.yaml` change that fires `generate-public-api-client.sh` here, so the C# client tracks the spec without a human resync. **Why deferred:** needs cross-repo PAT/permission plumbing; the manual resync + drift-check is the safe interim. This is what closes the gap between "same engine" and "same trigger".

## File structure

| Path | Create/Modify | Responsibility |
|---|---|---|
| `scripts/sync-upstream.sh` | Modify | Fix the missing `mkdir -p "$DEST"` for `peak-server-openapi`. |
| `upstream-snapshots/peak-server-openapi/public-api.yaml` | Create | Read-only spec mirror at the pinned tag. |
| `upstream-snapshots/peak-server-openapi/PIN.md` | Create | Records the pin tag + snapshot date + source path. |
| `upstream-snapshots/SOURCES.md` | Modify | Add the peak-server-openapi pin row. |
| `packages/peak-public-api-client-csharp/openapitools.json` | Create | Pins generator core to 7.9.0 (peak parity). |
| `packages/peak-public-api-client-csharp/openapi-config.yaml` | Create | Generator settings (csharp/restsharp/package name/TFMs/deterministic). |
| `packages/peak-public-api-client-csharp/.openapi-generator-ignore` | Create | Keeps only the generated sources; drops scaffolding. |
| `packages/peak-public-api-client-csharp/KyuzanInc.Peak.PublicApiClient.csproj` | Create | Hand-authored, CPM-aware, warning-relaxed project that compiles the generated sources. |
| `packages/peak-public-api-client-csharp/src/KyuzanInc.Peak.PublicApiClient/{Api,Client,Model}/*.cs` | Create (generated) | The client code — produced by the script, committed verbatim. |
| `packages/peak-public-api-client-csharp/packages.lock.json` | Create (generated) | Lock file for `--locked-mode` restores. |
| `scripts/generate-public-api-client.sh` | Create | The C# counterpart of peak's `openapi:generate`; drift CI runs it. |
| `Directory.Packages.props` | Modify | Re-pin the codegen dep group to the verified versions. |
| `peak-sdk-csharp.sln` | Modify | Add the client project so CI builds it. |
| `.editorconfig` | Modify | Mark the generated sources as generated code. |
| `.github/workflows/csharp-ci.yml` | Modify | Add the `openapi-client-drift` job; exclude generated code from the format check. |
| `docs/sync-rules.md` | Modify | Align the OpenAPI resync + drift sections with the real script and CI job. |
| `plans/plans-peak-sdk-csharp.md` | Modify | Flip W0c to Done and record the OQ-N1 provisional pin. |

---

### Task 1: Spec snapshot import + sync-upstream fix

**Files:**
- Modify: `scripts/sync-upstream.sh` (the `peak-server-openapi` case, around lines 57-68)
- Create: `upstream-snapshots/peak-server-openapi/public-api.yaml`
- Create: `upstream-snapshots/peak-server-openapi/PIN.md`
- Modify: `upstream-snapshots/SOURCES.md`

- [ ] **Step 1: Fix the `mkdir -p` gap in `scripts/sync-upstream.sh`**

In the `case "$NAME" in` block, change the `peak-server-openapi)` branch so it creates the destination directory before copying. Replace:

```bash
  peak-server-openapi)
    cp "apps/peak-public-docs/docs/api-references/public-api.yaml" "$DEST/public-api.yaml"
```

with:

```bash
  peak-server-openapi)
    mkdir -p "$DEST"
    cp "apps/peak-public-docs/docs/api-references/public-api.yaml" "$DEST/public-api.yaml"
```

- [ ] **Step 2: Import the v0.3.0 spec (branch-safe, no checkout switch)**

Run from the repo root. This clones peak read-only into a temp dir, extracts the tagged spec, and leaves the current branch untouched (unlike `sync-upstream.sh`, whose `git checkout -B` is for standalone resyncs):

```bash
TMP="$(mktemp -d)"
git clone --quiet git@github.com:KyuzanInc/peak.git "$TMP/peak"
git -C "$TMP/peak" checkout --quiet v0.3.0
mkdir -p upstream-snapshots/peak-server-openapi
cp "$TMP/peak/apps/peak-public-docs/docs/api-references/public-api.yaml" \
   upstream-snapshots/peak-server-openapi/public-api.yaml
rm -rf "$TMP"
```

Expected: `upstream-snapshots/peak-server-openapi/public-api.yaml` exists (~1480 lines).

- [ ] **Step 3: Write the pin record `upstream-snapshots/peak-server-openapi/PIN.md`**

```markdown
# peak-server OpenAPI snapshot

| Pin tag | Snapshot date | Source path |
|---|---|---|
| `v0.3.0` | 2026-05-29 | KyuzanInc/peak `apps/peak-public-docs/docs/api-references/public-api.yaml` |

> Provisional pin (OQ-N1). Confirm with Komy CTO that `v0.3.0` is the
> intended public-API baseline before v0.1.0 ships. Re-sync with
> `scripts/sync-upstream.sh peak-server-openapi <tag>`.
```

- [ ] **Step 4: Add the pin row to `upstream-snapshots/SOURCES.md`**

Read the file first, then add a row to the sources table for `peak-server-openapi` → `KyuzanInc/peak` @ `v0.3.0`, source path `apps/peak-public-docs/docs/api-references/public-api.yaml`, snapshot date `2026-05-29`. Match the existing table's columns exactly.

- [ ] **Step 5: Verify the spec has the endpoints the client needs**

Run: `grep -cE "init-otp-login|complete-otp-login|/accounts|/private-keys" upstream-snapshots/peak-server-openapi/public-api.yaml`
Expected: a non-zero count (≥ 8).

- [ ] **Step 6: Commit**

```bash
git add scripts/sync-upstream.sh upstream-snapshots/peak-server-openapi/ upstream-snapshots/SOURCES.md
git commit -m "feat(openapi): pin peak-server public-api.yaml @ v0.3.0 + fix sync-upstream mkdir"
```

---

### Task 2: Generator config, ignore file, and generation script

**Files:**
- Create: `packages/peak-public-api-client-csharp/openapitools.json`
- Create: `packages/peak-public-api-client-csharp/openapi-config.yaml`
- Create: `packages/peak-public-api-client-csharp/.openapi-generator-ignore`
- Create: `scripts/generate-public-api-client.sh`

- [ ] **Step 1: Create `packages/peak-public-api-client-csharp/openapitools.json`**

```json
{
  "spaces": 2,
  "generator-cli": {
    "version": "7.9.0"
  }
}
```

- [ ] **Step 2: Create `packages/peak-public-api-client-csharp/openapi-config.yaml`**

```yaml
# Generator config for the internal Peak Public API C# client.
# Engine + core version are pinned by ./openapitools.json (7.9.0), matching
# peak-server's generator. The restsharp library matches the runtime deps
# pinned in Directory.Packages.props. JSON is Newtonsoft (the restsharp
# library ignores useSystemTextJson in 7.9.0). hideGenerationTimestamp keeps
# the output byte-stable so drift CI is meaningful.
generatorName: csharp
library: restsharp
additionalProperties:
  packageName: KyuzanInc.Peak.PublicApiClient
  targetFramework: net8.0;netstandard2.1
  hideGenerationTimestamp: true
```

- [ ] **Step 3: Create `packages/peak-public-api-client-csharp/.openapi-generator-ignore`**

```
# Keep ONLY the generated C# sources under
# src/KyuzanInc.Peak.PublicApiClient/. Everything else the csharp generator
# emits (project scaffolding, docs, the sample .Test project, the vestigial
# git_push.sh / appveyor.yml) is ignored, so our hand-authored, CPM-aware
# csproj and the repo's own tooling own the build.
*.csproj
*.sln
src/**/*.csproj
appveyor.yml
git_push.sh
README.md
.gitignore
docs/
api/
src/KyuzanInc.Peak.PublicApiClient.Test/
```

- [ ] **Step 4: Create `scripts/generate-public-api-client.sh`**

```bash
#!/usr/bin/env bash
# Regenerate the internal C# OpenAPI client from the pinned peak-server spec.
#
# This is the C# counterpart of peak's `pnpm --filter peak-server
# openapi:generate`: same engine (@openapitools/openapi-generator-cli, core
# pinned to 7.9.0 via openapitools.json), same source spec, emitting the
# `csharp` generator instead of typescript-axios. Drift CI runs this and
# fails if the committed client changes, so the output must stay
# deterministic (hideGenerationTimestamp=true in openapi-config.yaml).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PKG="$REPO_ROOT/packages/peak-public-api-client-csharp"
SPEC="$REPO_ROOT/upstream-snapshots/peak-server-openapi/public-api.yaml"

if [ ! -f "$SPEC" ]; then
  echo "ERROR: spec not found at $SPEC" >&2
  echo "Run: scripts/sync-upstream.sh peak-server-openapi <tag>" >&2
  exit 1
fi

# clean:generated — drop only the generated sources; keep the hand-authored
# csproj / config / .openapi-generator-ignore that live alongside them.
rm -rf "$PKG/src/KyuzanInc.Peak.PublicApiClient/Api" \
       "$PKG/src/KyuzanInc.Peak.PublicApiClient/Client" \
       "$PKG/src/KyuzanInc.Peak.PublicApiClient/Model" \
       "$PKG/.openapi-generator"

# cd into the package so the wrapper reads its openapitools.json (pins 7.9.0).
cd "$PKG"
npx --yes @openapitools/openapi-generator-cli@2.22.0 generate \
  -g csharp \
  -i "$SPEC" \
  -o "$PKG" \
  -c "$PKG/openapi-config.yaml"

# Fail loud if the wrapper ever ran a different core than the pinned 7.9.0
# (e.g. a stale npx cache or a missing openapitools.json) — otherwise drift CI
# could silently compare output from two generator versions.
EXPECTED_VERSION="7.9.0"
ACTUAL_VERSION="$(cat "$PKG/.openapi-generator/VERSION" 2>/dev/null || echo "MISSING")"
if [ "$ACTUAL_VERSION" != "$EXPECTED_VERSION" ]; then
  echo "ERROR: generator core was $ACTUAL_VERSION, expected $EXPECTED_VERSION (check openapitools.json)." >&2
  exit 1
fi

echo "Generated C# client into $PKG/src/KyuzanInc.Peak.PublicApiClient/ (generator $ACTUAL_VERSION)"
```

- [ ] **Step 5: Make the script executable**

Run: `chmod +x scripts/generate-public-api-client.sh`

- [ ] **Step 6: Commit**

```bash
git add packages/peak-public-api-client-csharp/openapitools.json \
        packages/peak-public-api-client-csharp/openapi-config.yaml \
        packages/peak-public-api-client-csharp/.openapi-generator-ignore \
        scripts/generate-public-api-client.sh
git commit -m "feat(openapi): add csharp client generator config + generation script"
```

---

### Task 3: Generate the client, wire the project, build green

**Files:**
- Modify: `Directory.Packages.props` (the `CodegenAndTooling` ItemGroup, lines 47-52)
- Create: `packages/peak-public-api-client-csharp/KyuzanInc.Peak.PublicApiClient.csproj`
- Create: `packages/peak-public-api-client-csharp/.gitattributes`
- Create (generated): `packages/peak-public-api-client-csharp/src/KyuzanInc.Peak.PublicApiClient/**`
- Create (generated): `packages/peak-public-api-client-csharp/packages.lock.json`
- Modify: `peak-sdk-csharp.sln`
- Modify: `.editorconfig`

- [ ] **Step 1: Re-pin the codegen deps in `Directory.Packages.props`**

Replace the existing `CodegenAndTooling` ItemGroup:

```xml
  <ItemGroup Label="CodegenAndTooling">
    <!-- For OpenAPI codegen artifacts; pulled by the generator -->
    <PackageVersion Include="JsonSubTypes" Version="2.0.1" />
    <PackageVersion Include="Polly" Version="7.2.4" />
    <PackageVersion Include="RestSharp" Version="111.4.1" />
  </ItemGroup>
```

with the verified set (matches the `csharp`/`restsharp` 7.9.0 output; RestSharp 112.0.0 fixes GHSA-4rr6-2v9v-wcpc present in 111.4.1):

```xml
  <ItemGroup Label="CodegenAndTooling">
    <!--
      Runtime deps of the internal KyuzanInc.Peak.PublicApiClient (OpenAPI
      csharp generator 7.9.0, restsharp library). Versions match the
      generator's own pins. RestSharp 112.0.0 fixes GHSA-4rr6-2v9v-wcpc
      (the advisory present in 111.4.1). JSON is Newtonsoft (the restsharp
      library does not honour useSystemTextJson in 7.9.0).
      System.ComponentModel.Annotations is required for the DataAnnotations
      attributes on the netstandard2.1 target (net8.0 has them built in).
    -->
    <PackageVersion Include="RestSharp" Version="112.0.0" />
    <PackageVersion Include="Polly" Version="8.1.0" />
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
    <PackageVersion Include="System.ComponentModel.Annotations" Version="5.0.0" />
  </ItemGroup>
```

- [ ] **Step 2: Create the hand-authored project `packages/peak-public-api-client-csharp/KyuzanInc.Peak.PublicApiClient.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net8.0;netstandard2.1</TargetFrameworks>
    <AssemblyName>KyuzanInc.Peak.PublicApiClient</AssemblyName>
    <RootNamespace>KyuzanInc.Peak.PublicApiClient</RootNamespace>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>

    <!--
      Generated OpenAPI code: relax the repo-wide strictness from the root
      Directory.Build.props so the output builds clean. CS1591 = missing XML
      doc, CS8019 = unnecessary using (the generator emits an unused
      `using System.Web;`), CS0618 = RestSharp 112 obsolete members.
    -->
    <Nullable>annotations</Nullable>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <NoWarn>$(NoWarn);CS1591;CS8019;CS0618</NoWarn>

    <!-- Internal-only, never published (D13). -->
    <IsPackable>false</IsPackable>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>

    <!--
      Compile only the generated client sources. The generator can also emit
      a sample .Test project; scoping the glob to the client directory keeps
      it out even if .openapi-generator-ignore ever changes.
    -->
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="src/KyuzanInc.Peak.PublicApiClient/**/*.cs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="RestSharp" />
    <PackageReference Include="Polly" />
    <PackageReference Include="Newtonsoft.Json" />
    <PackageReference Include="System.ComponentModel.Annotations" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2b: Pin LF line endings for the generated tree**

The generator writes LF, but a contributor on Windows with `core.autocrlf=true` could commit CRLF, which would then read as drift on the LF-normalised CI runner. Create `packages/peak-public-api-client-csharp/.gitattributes` so the stored bytes are deterministic regardless of OS. (The generator never emits `.gitattributes`, so this hand-authored file is safe.)

```
# Generated OpenAPI client — store with LF so regeneration is byte-stable
# across machines and the drift-check job never trips on line endings.
* text=auto eol=lf
*.cs text eol=lf
```

- [ ] **Step 3: Generate the client**

Run: `./scripts/generate-public-api-client.sh`
Expected (last line): `Generated C# client into .../packages/peak-public-api-client-csharp/src/KyuzanInc.Peak.PublicApiClient/`

- [ ] **Step 4: Verify only the intended files were written**

Run:
```bash
find packages/peak-public-api-client-csharp -type f \
  -not -path '*/.openapi-generator/*' \
  \( -name '*.csproj' -o -name '*.sln' -o -path '*/docs/*' -o -path '*/api/*' \
     -o -name 'appveyor.yml' -o -name 'git_push.sh' -o -path '*.Test/*' \) \
  | grep -v 'KyuzanInc.Peak.PublicApiClient.csproj' || echo "CLEAN"
```
Expected: `CLEAN` (the only `.csproj` is our hand-authored one at the package root).

- [ ] **Step 5: Mark the generated sources as generated code in `.editorconfig`**

Append to `.editorconfig`. `generated_code = true` is the primary, documented way to make `dotnet format` (and Roslyn analyzers) skip these files — the generator's `/* ... */` header is not the `// <auto-generated/>` marker the tooling recognises, so the editorconfig flag is what does the work. Use the conventional `**/*.cs` glob:

```ini
# Generated OpenAPI client — treat as generated so dotnet format / analyzers
# skip it (the output does not match the repo's C# style rules).
[packages/peak-public-api-client-csharp/src/**/*.cs]
generated_code = true
```

- [ ] **Step 6: Add the project to the solution**

Run: `dotnet sln peak-sdk-csharp.sln add packages/peak-public-api-client-csharp/KyuzanInc.Peak.PublicApiClient.csproj`
Expected: `Project ... added to the solution.`

- [ ] **Step 7: Restore (writes the lock file) and build the whole solution**

Run:
```bash
dotnet restore peak-sdk-csharp.sln
dotnet build peak-sdk-csharp.sln -c Release --no-restore
```
Expected: `Build succeeded`, **0 Warning(s) 0 Error(s)**. A `packages/peak-public-api-client-csharp/packages.lock.json` now exists.

- [ ] **Step 8: Confirm locked-mode restore is satisfied (matches CI)**

Run: `dotnet restore peak-sdk-csharp.sln --locked-mode`
Expected: success with no "lock file is out of date" error. Both CI jobs that restore the solution — `build-and-test` and `consumer-restore-check` — use `--locked-mode`, so the committed `packages.lock.json` from Step 7 is what keeps both green; a half-commit that adds the project without the lock file would break both.

- [ ] **Step 8b: Confirm the format check passes locally (matches the CI Format step)**

Run: `dotnet format peak-sdk-csharp.sln --verify-no-changes --no-restore`
Expected: exits 0 with no reported changes. This proves the `generated_code = true` editorconfig flag (Step 5) actually makes `dotnet format` skip the generated sources, BEFORE CI runs it. If it reports changes under `packages/peak-public-api-client-csharp/`, the editorconfig glob is not matching — fix the glob (it must be `[packages/peak-public-api-client-csharp/src/**/*.cs]`) and re-run; do not "format" the generated files.

- [ ] **Step 9: Confirm the existing test suite is unaffected**

Run: `dotnet test peak-sdk-csharp.sln -c Release --no-build --filter "Category!=E2E"`
Expected: the existing tests pass; the new project contributes no tests.

- [ ] **Step 10: Commit**

```bash
git add Directory.Packages.props .editorconfig peak-sdk-csharp.sln \
        packages/peak-public-api-client-csharp/KyuzanInc.Peak.PublicApiClient.csproj \
        packages/peak-public-api-client-csharp/.gitattributes \
        packages/peak-public-api-client-csharp/src \
        packages/peak-public-api-client-csharp/.openapi-generator \
        packages/peak-public-api-client-csharp/packages.lock.json
git commit -m "feat(openapi): generate internal KyuzanInc.Peak.PublicApiClient from pinned spec"
```

---

### Task 4: Drift-check CI + format-step exclusion

**Files:**
- Modify: `.github/workflows/csharp-ci.yml`

- [ ] **Step 1: Exclude generated code from the format check (defense-in-depth)**

The `generated_code = true` editorconfig flag (Task 3 Step 5) is the primary mechanism and is verified locally in Task 3 Step 8b. Add `--exclude` as a second, independent guard so the format step stays green even if a future SDK changes how it honours `generated_code`. In the existing `build-and-test` job, change the Format check step from:

```yaml
      - name: Format check
        if: matrix.os == 'ubuntu-latest'
        run: dotnet format peak-sdk-csharp.sln --verify-no-changes --no-restore
```

to:

```yaml
      - name: Format check
        if: matrix.os == 'ubuntu-latest'
        run: dotnet format peak-sdk-csharp.sln --verify-no-changes --no-restore --exclude packages/peak-public-api-client-csharp/
```

- [ ] **Step 2: Add the `openapi-client-drift` job**

Append this job under `jobs:` in `.github/workflows/csharp-ci.yml` (sibling of `build-and-test` and `consumer-restore-check`):

```yaml
  openapi-client-drift:
    name: openapi client drift
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup JRE (openapi-generator runs on Java)
        uses: actions/setup-java@v4
        with:
          distribution: temurin
          java-version: '17'
      - uses: actions/setup-node@v4
        with:
          node-version: '22'
      - name: Regenerate the C# client from the pinned spec
        run: ./scripts/generate-public-api-client.sh
      - name: Fail if the committed client drifted from the spec
        run: |
          if ! git diff --quiet -- \
              packages/peak-public-api-client-csharp/src \
              packages/peak-public-api-client-csharp/.openapi-generator; then
            echo "::error::Generated client is out of sync with the pinned spec. Run scripts/generate-public-api-client.sh and commit the result."
            git --no-pager diff --stat -- \
              packages/peak-public-api-client-csharp/src \
              packages/peak-public-api-client-csharp/.openapi-generator
            exit 1
          fi
          echo "No drift."
```

- [ ] **Step 3: Prove the drift job PASSES on a clean tree (locally)**

Run:
```bash
./scripts/generate-public-api-client.sh
git diff --quiet -- packages/peak-public-api-client-csharp/src packages/peak-public-api-client-csharp/.openapi-generator && echo "NO DRIFT (good)"
```
Expected: `NO DRIFT (good)` — regeneration is byte-stable.

- [ ] **Step 4: Prove the drift job CATCHES drift (locally), then revert**

Run:
```bash
echo "// drift probe" >> packages/peak-public-api-client-csharp/src/KyuzanInc.Peak.PublicApiClient/Client/ApiClient.cs
git diff --quiet -- packages/peak-public-api-client-csharp/src && echo "MISSED (bad)" || echo "CAUGHT (good)"
git checkout -- packages/peak-public-api-client-csharp/src/KyuzanInc.Peak.PublicApiClient/Client/ApiClient.cs
```
Expected: `CAUGHT (good)`, then the probe is reverted.

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/csharp-ci.yml
git commit -m "ci: add openapi client drift-check job + exclude generated code from format"
```

---

### Task 5: Documentation + status alignment

**Files:**
- Modify: `docs/sync-rules.md`
- Modify: `plans/plans-peak-sdk-csharp.md`
- Modify: `CLAUDE.md`

- [ ] **Step 1: Fix the `peak-server-openapi` resync step in `docs/sync-rules.md`**

Read the file. In the "Post-sync mandatory steps → `peak-server-openapi` resync" section, replace the `pnpm openapi-generator-cli generate` instruction with the real command. Change:

```markdown
- Re-generate `packages/peak-public-api-client-csharp/src/` with
  `pnpm openapi-generator-cli generate` (or the project-local
  equivalent — codegen settings live in
  `packages/peak-public-api-client-csharp/openapi-config.yaml`).
```

to:

```markdown
- Re-generate the client with `scripts/generate-public-api-client.sh`
  (engine: `@openapitools/openapi-generator-cli`, core pinned to 7.9.0 by
  `packages/peak-public-api-client-csharp/openapitools.json`; settings in
  `packages/peak-public-api-client-csharp/openapi-config.yaml`).
- Build the client and commit the regenerated
  `packages/peak-public-api-client-csharp/src/` plus the refreshed
  `packages.lock.json`.
```

- [ ] **Step 2: Correct the "Drift CI" section in `docs/sync-rules.md`**

Replace the entire existing `## Drift CI` section (it currently describes a `drift-check` job that "Diffs ... against the live spec at the pinned peak-server tag", is "informative only", and "requires an explicit 'resync intended' label" — none of which is true of the real job). Delete all of that and replace with the text below. The real job verifies committed-client-vs-committed-spec only (it does NOT clone peak — comparing the snapshot to the live tag stays a manual, auth'd operator step), and it hard-fails:

```markdown
## Drift CI

`.github/workflows/csharp-ci.yml` runs an `openapi-client-drift` job that:

- Regenerates the C# client from the committed
  `upstream-snapshots/peak-server-openapi/public-api.yaml` using
  `scripts/generate-public-api-client.sh`.
- Fails the build if the committed
  `packages/peak-public-api-client-csharp/src/` differs from the
  regenerated output, naming the drift in the log.

The job needs a JRE + Node (the generator is Java, launched via `npx`). It
does **not** fetch peak; comparing the snapshot against the live
`peak-server` tag is a manual operator step via
`scripts/sync-upstream.sh peak-server-openapi <tag>`.
```

- [ ] **Step 3: Flip W0c to Done in `plans/plans-peak-sdk-csharp.md`**

Read the file. In the Status overview table, replace the W0c row:

```markdown
| W0c | OpenAPI sync workflow + drift CI from peak-server tag | ⬜ Deferred to PR 2 follow-up | OpenAPI codegen not landed yet (OQ-N1 open) | — | Codex D-P1 |
```

with:

```markdown
| W0c | OpenAPI sync workflow + drift CI from peak-server tag | ✅ Done | `scripts/generate-public-api-client.sh` + `openapi-client-drift` CI job; client generated from `peak-server` @ v0.3.0 | (this PR) | Codex D-P1 |
```

- [ ] **Step 4: Update OQ-N1 in `plans/plans-peak-sdk-csharp.md`**

In the Open questions table, change the OQ-N1 Status cell from `⬜ Open` to:

```markdown
🟡 Provisionally pinned to `v0.3.0` (latest tag, has the PR2 endpoints); Komy CTO to confirm before v0.1.0
```

- [ ] **Step 4b: Record the Option A vs B decision in `plans/plans-peak-sdk-csharp.md`**

A future reader will ask "the user wanted peak's own trigger to emit C# — why didn't we do that?" Capture the answer so it is not re-litigated. In the "Clarified decisions" section, add a new entry after D23:

```markdown
- **D24 (new)** — C# OpenAPI client generation runs **in this repo**
  (Option B), not as an extra generator inside peak's `openapi:generate`
  (Option A). Same engine (`openapi-generator-cli` core `7.9.0`) and same
  source spec, but the C# client is generated here from the synced
  snapshot and drift-checked in CI. Option A was declined because the C#
  client's output belongs to this repo, peak is a Node/Nest repo (adding a
  C# artifact crosses the repo boundary D23 draws), and the snapshot model
  keeps peak free of C#-build concerns. **Accepted cost:** the trigger is an
  operator-run resync, not automatic on a peak merge; automatic cross-repo
  dispatch is the deferred B+ follow-up.
```

- [ ] **Step 4c: Add the client's lock file to the auto-generated list in `CLAUDE.md`**

`CLAUDE.md` already names the two existing `packages.lock.json` files under "Auto-generated code". Read the file and add the new one to that bullet so the "commit alongside" rule covers it. Change:

```markdown
- `packages/peak-sdk-csharp/src/packages.lock.json` and
  `packages/peak-sdk-csharp/tests/packages.lock.json` — generated by
  `dotnet restore`; commit alongside any `Directory.Packages.props`
  change.
```

to add the client's lock file (keep the existing two):

```markdown
- `packages/peak-sdk-csharp/src/packages.lock.json`,
  `packages/peak-sdk-csharp/tests/packages.lock.json`, and
  `packages/peak-public-api-client-csharp/packages.lock.json` — generated
  by `dotnet restore`; commit alongside any `Directory.Packages.props`
  change.
```

- [ ] **Step 5: Commit**

```bash
git add docs/sync-rules.md plans/plans-peak-sdk-csharp.md CLAUDE.md
git commit -m "docs: align sync-rules + W0c status + A/B decision with the landed csharp codegen"
```

---

## Self-Review

**Spec coverage — and an honest read of "same trigger"** — the user's words were "generate the C# public-api-client with basically the same trigger as peak." What this plan delivers is **same engine + same source spec, different trigger mechanism**: same `openapi-generator-cli` core `7.9.0` (Task 2 `openapitools.json`), same spec mirrored at a tag (Task 1), and a `generate-public-api-client.sh` that mirrors peak's `openapi:generate` clean+generate (Task 2). It is NOT peak's trigger: peak regenerates its client in the same merge that changes the spec, whereas here an operator must run `sync-upstream.sh` to refresh the snapshot before the client regenerates. The drift-check (Task 4) only guards committed-client-vs-committed-spec; a peak-side spec change produces **no signal** in this repo until someone resyncs. That is a deliberate consequence of the repo-boundary decision (D24), not full parity. The automatic-on-peak-merge trigger is the deferred B+ follow-up below — do not read this plan as having shipped trigger parity.

**Placeholder scan** — no TBDs. Every config file, the csproj, the script, the CPM diff, the CI job, and the doc edits are given verbatim and were trial-validated. Generated `.cs` files are intentionally not transcribed (they are produced by the exact command in Task 3 Step 3 and committed verbatim — that is the nature of generated code, not a placeholder).

**Type/name consistency** — the package/assembly/namespace `KyuzanInc.Peak.PublicApiClient`, the output path `packages/peak-public-api-client-csharp/`, the spec path `upstream-snapshots/peak-server-openapi/public-api.yaml`, and the script name `scripts/generate-public-api-client.sh` are used identically across the generator config, the csproj glob, the ignore file, the CI job, and the docs.

**Known residual risks (flagged, not blocking):**
- Format check: `.editorconfig generated_code=true` is the primary guard and is now verified **locally** in Task 3 Step 8b (`dotnet format --verify-no-changes`) before any commit; `--exclude` in CI (Task 4) is a second independent guard. If the local verify fails, the glob is fixed before landing — CI is not the first place this is exercised.
- Determinism: regeneration is pinned (generator core 7.9.0 asserted by the script, `hideGenerationTimestamp=true`) and line endings are pinned (`.gitattributes` `eol=lf`), so the drift job compares like-for-like across macOS dev and ubuntu CI. The only network dependency is the `npx`-download of the version-pinned generator.
- The pin is `v0.3.0` pending OQ-N1; if Komy CTO names a different tag, re-run Task 1 Step 2-3 with that tag and Task 3 Step 3 (regenerate) — no structural change.
- Trigger parity: this ships same-engine/same-spec, operator-triggered (see Self-Review spec-coverage note and Follow-up 2). Not a residual bug — a scoped decision (D24) — but flagged so nobody assumes peak-merge auto-regen exists.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-29-csharp-public-api-client-codegen.md`. Two execution options:

1. **Subagent-Driven (recommended)** — a fresh subagent per task, two-stage review between tasks, fast iteration.
2. **Inline Execution** — execute tasks in this session with checkpoints for review.

Per the session goal, execution proceeds inline (the implementer already has every artifact empirically validated), then `/ship`, iterating until CI and review are clean.

## Implementation amendments (post-landing)

Two refinements were made during execution; they supersede the matching snippets above:

1. **Generated-client deps use `VersionOverride`, not central `PackageVersion` (NU1004 fix).** Task 3 Step 1 originally added `Newtonsoft.Json` and `System.ComponentModel.Annotations` to the central `CodegenAndTooling` group. With `CentralPackageTransitivePinningEnabled=true` those are transitive deps of `peak-sdk-csharp` and its tests, so the central pins invalidated their committed lock files (NU1004) and would have re-pinned the SDK's resolved graph. The shipped fix removes the central `CodegenAndTooling` group entirely and gives the client its versions via `VersionOverride` in its csproj, scoping them to that one isolated, never-published project. SDK/tests lock files stay valid (those deps remain `Transitive`/floating). Caught by CI `build-and-test`/`consumer-restore-check` (`--locked-mode`), fixed, re-verified green.

2. **Drift gate uses `git add -A` + `git diff --cached` (catches additions).** Task 4's gate used plain `git diff`, which only inspects tracked files — a spec change that ADDS a new model/endpoint emits a brand-new untracked `.cs` that `git diff` would miss (a false green, saved only accidentally by the rewritten `.openapi-generator/FILES`). The shipped job stages all changes first so additions and deletions are first-class. Verified locally: no-drift → clean; a new untracked file → caught.

3. **Pin tracks `main`, not a release tag (user directive, OQ-N1 resolved).** Task 1 pinned the spec to tag `v0.3.0`; per the user it now tracks `KyuzanInc/peak` `main` HEAD instead. The snapshot is re-synced from `main` and PIN.md records the exact HEAD commit (`72ca08b3…`) so it stays reproducible while following main. Resync is `scripts/sync-upstream.sh peak-server-openapi main`. Continuous auto-following (a scheduled resync job) is the open decision; the manual/operator resync is the interim, same as before.

## GSTACK REVIEW REPORT

| Review | Trigger | Why | Runs | Status | Findings |
|--------|---------|-----|------|--------|----------|
| CEO Review | `/plan-ceo-review` | Scope & strategy | 1 | CLEAR | 5 raised (2 P1, 2 P2, 1 P3); all applied |
| Eng Review | `/plan-eng-review` | Architecture & tests (required) | 1 | CLEAR | 7 raised (2 P1, 3 P2, 2 P3); all applied or deferred-by-agreement |
| Outside Voice | independent subagents | 2nd-context challenge | 2 | CLEAR | served by the two independent review subagents |
| Design Review | `/plan-design-review` | UI/UX gaps | 0 | N/A | no UI surface in this plan |
| DX Review | `/plan-devex-review` | Developer-experience gaps | 0 | N/A | not run |

**Findings applied (Eng):** format-check exclusion made robust (`generated_code=true` glob `**/*.cs` + local `dotnet format` verify in Task 3 Step 8b + CI `--exclude`); lock file noted as gating both `build-and-test` and `consumer-restore-check`; `.gitattributes` added to pin LF and prevent CRLF drift (Task 3 Step 2b); generator `VERSION==7.9.0` assertion added to the script; `sync-rules.md` Drift-CI replacement made precise (delete the stale `drift-check`/live-tag/"informative only" text); client lock file added to `CLAUDE.md` (Task 5 Step 4c). Dismissed by agreement: README/`InternalsVisibleTo` (not needed for an unreferenced internal lib).

**Findings applied (CEO):** "same trigger" claim corrected to "same engine + same spec, operator-triggered" with the gap stated (Self-Review + residual risks); B+ auto-dispatch and consumer-wiring captured as named Follow-ups; Option A-vs-B rationale recorded as D24 (Task 5 Step 4b). Provisional `v0.3.0` pin confirmed acceptable (reversible, does not gate the pipeline); innovation-token spend (Java in CI) judged reasonable.

**CROSS-MODEL:** both reviewers independently endorsed the core design (Option B, restsharp/7.9.0, internal-only, drift-checked) and independently flagged the "same trigger" overclaim — high-confidence signal that the honesty fix was the right call.

**UNRESOLVED:** none blocking. External dependency OQ-N1 (Komy CTO confirms the spec tag) is tracked and reversible; it does not gate implementation.

**VERDICT:** CEO + ENG CLEARED — ready to implement. Every shipped artifact (generation, ignore-filter, both-TFM build at 0 warnings, determinism) was validated locally before this report.
