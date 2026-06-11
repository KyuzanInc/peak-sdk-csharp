# Unity reference example (OTP login, IL2CPP smoke) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `examples/peak-sdk-unity-reference/` — a minimal Unity 6000.0.x project that consumes `KyuzanInc.Peak.Sdk` via a local `.nupkg` feed and demonstrates the full OTP login + wallet import/export flow through an IMGUI UI, as a human-run IL2CPP AOT smoke.

**Architecture:** The example is a standalone Unity project, **not** part of `peak-sdk-csharp.sln`. It consumes the working-tree SDK as an external NuGet consumer would: `dotnet pack` produces `KyuzanInc.Peak.Sdk.nupkg` into a project-local `LocalFeed/`, the transitive `KyuzanInc.Turnkey.Sdk` nupkg is vendored beside it (no GitHub auth at Unity-restore time), and NuGetForUnity restores the full dependency closure listed in `Assets/packages.config`. A MonoBehaviour drives an IMGUI state machine over the SDK. The one in-repo automated guard is a compile-only API-conformance test in the existing SDK test project; everything Unity-specific (open, Play-mode, IL2CPP build) is a human checklist.

**Tech Stack:** Unity 6000.0.x (IL2CPP, .NET Standard 2.1 API level), NuGetForUnity 4.x, `KyuzanInc.Peak.Sdk` 0.1.0-alpha.2 (+ transitive `KyuzanInc.Turnkey.Sdk`, System.Text.Json 8.0.5, Microsoft.Extensions.*, BouncyCastle.Cryptography), C# (the SDK netstandard2.1 surface), xUnit (the conformance test), bash (`prepare-feed.sh`).

**Design source (normative):** `docs/superpowers/specs/2026-06-09-unity-reference-example-otp-login-design.md` (r2, review-cleared). Read it first; this plan implements its decisions D1–D13.

> **Superseded in part (2026-06-11, spec r4):** the local `.nupkg` feed + `prepare-feed.sh` in Task 2 (and the Goal/Architecture above) were **removed**. Internal testing now consumes `KyuzanInc.Peak.Sdk` + `KyuzanInc.Turnkey.Sdk` from **GitHub Packages** (`github-kyuzan`) declared in `Assets/NuGet.config` (credential from the global `~/.nuget` config). Task 2's local-feed plumbing is historical — see spec §14 r4 + the example `README.md` for the current setup. Also: `ProjectVersion.txt` is pinned to **6000.0.73f1** and the project was validated to open + restore + compile (0 errors) via the Editor CLI (see the smoke log); NuGetForUnity reads `Assets/NuGet.config` (not a project-root `NuGet.config`), and its auto-restore needs a clean compile first (move `Assets/Scripts` aside, restore, move back).

**Key constraints (from the spec, do not violate):**
- `chainType` for `CompleteImportPrivateKeyAsync` is exactly `"evm"` or `"solana"` — **never `"ETHEREUM"`** (D6). Use the shared `ChainTypeEvm` constant.
- The `projectApiKey` is **never** a `[SerializeField]` (it would serialize into the committed scene). Runtime entry only; committed scene is key-free (D9).
- `async void` handlers: mandatory `try/catch`, **no `ConfigureAwait(false)`**, `_busy` single-flight guard (D7).
- Never echo raw key material / request bodies to the UI log or `Debug.Log*` — redacted markers only (D9).
- `Assets/packages.config` enumerates the **complete** dependency graph (NFU is not transitive), generated from `packages/peak-sdk-csharp/src/packages.lock.json` (D8).
- Verify locally per `docs/development.md` (GitHub Packages auth set up): `dotnet restore peak-sdk-csharp.sln --locked-mode` → `dotnet build -c Release` → `dotnet test -c Release --filter "Category!=E2E"` stays green; `dotnet format peak-sdk-csharp.sln --verify-no-changes` clean.

---

## File Structure

**New (committed):**
- `examples/peak-sdk-unity-reference/.gitignore` — Unity-generated churn + `Assets/Packages/` + `LocalFeed/`
- `examples/peak-sdk-unity-reference/README.md` — setup (no-auth default + GitHub-auth alt), test-creds warning, run + smoke steps
- `examples/peak-sdk-unity-reference/prepare-feed.sh` — pack the csproj + vendor the exact-pinned Turnkey nupkg
- `examples/peak-sdk-unity-reference/NuGet.config` — LocalFeed (`KyuzanInc.*`) + nuget.org (`*`)
- `examples/peak-sdk-unity-reference/Assets/packages.config` (+ `.meta`) — full closure, pinned exact
- `examples/peak-sdk-unity-reference/Assets/link.xml` (+ `.meta`) — preserve BouncyCastle + Turnkey (+ SDK/STJ) under IL2CPP
- `examples/peak-sdk-unity-reference/Assets/Scripts/PeakExampleDemo.cs` (+ `.meta`) — the MonoBehaviour
- `examples/peak-sdk-unity-reference/Assets/Scripts/Peak.Example.asmdef` (+ `.meta`) — asmdef
- `examples/peak-sdk-unity-reference/Assets/Scripts.meta` — Scripts folder meta
- `examples/peak-sdk-unity-reference/Assets/Scenes/SampleScene.unity` (+ `.meta`) — one scene, a GameObject carrying the MonoBehaviour, NO key
- `examples/peak-sdk-unity-reference/Assets/Scenes.meta` — Scenes folder meta
- `examples/peak-sdk-unity-reference/Packages/manifest.json` — NFU pinned via git-URL UPM
- `examples/peak-sdk-unity-reference/ProjectSettings/ProjectVersion.txt` — Unity version pin
- `examples/peak-sdk-unity-reference/ProjectSettings/ProjectSettings.asset` — IL2CPP + .NET Standard 2.1 + stripping (seed; Unity normalizes)
- `examples/peak-sdk-unity-reference/ProjectSettings/EditorBuildSettings.asset` — SampleScene in the build list (seed)
- `examples/peak-sdk-unity-reference/ProjectSettings/EditorSettings.asset` — Force-Text serialization (seed; D5 determinism)
- `packages/peak-sdk-csharp/tests/ExampleApiConformanceTests.cs` — compile-only API-conformance guard (D13)
- `docs/operations/il2cpp-smoke-2026-06-09.md` — smoke-log template, link.xml pre-seeded

**Modified:**
- `CLAUDE.md` — scope the `UnityEngine` rule to shipped packages; permit `examples/**` (D10)
- `plans/plans-peak-sdk-csharp.md` — W3 row (un-stale) + PR-3 narrative (scope widening)
- `docs/development.md` — examples pointer + NFU/version reconcile + no-auth local-feed path
- `README.md` (repo root) — one-line example pointer

**Generated (gitignored, never committed):** `Library/`, `Temp/`, `Logs/`, `obj/`, `Build/`, `UserSettings/`, Unity `*.csproj`/`*.sln`, `Assets/Packages/` (+ `.meta`), `LocalFeed/`.

> **Unity-YAML note (read once):** The agent implementing this plan cannot run Unity. Tasks 3–4 commit hand-authored **seed** versions of the Unity-generated files (`ProjectSettings.asset`, `EditorBuildSettings.asset`, `SampleScene.unity`) with the load-bearing fields and fixed `.meta` GUIDs, so the project opens. Task 8 (human) opens the project in Unity once, lets Unity normalize/complete those files and generate the rest of the standard `ProjectSettings/` set, verifies the scene is key-free, runs the smoke, and commits the Unity-normalized files + the smoke log. This division is intentional and honest (spec §1.4/§6).

---

## Task 1: Scaffold directories, example `.gitignore`, `docs/operations/`

**Files:**
- Create: `examples/peak-sdk-unity-reference/.gitignore`
- Create: `docs/operations/.gitkeep`

- [ ] **Step 1: Create the example `.gitignore`** (GitHub official `Unity.gitignore` base + NFU/feed appends, D5)

Create `examples/peak-sdk-unity-reference/.gitignore`:

```gitignore
# Unity-generated (based on github/gitignore Unity.gitignore)
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/
[Mm]emoryCaptures/
[Rr]ecordings/

# Asset meta data only kept for committed assets above; restored NuGet DLLs are NOT committed
/Assets/Packages/
/Assets/Packages.meta

# Local NuGet feed produced by prepare-feed.sh (binaries, not committed)
/LocalFeed/

# Unity-generated solution/project files
*.csproj
*.sln
*.unityproj
*.suo
*.user
*.userprefs
*.pidb
*.booproj
*.svd
*.pdb
*.mdb
*.opendb
*.VC.db

# OS / editor
.DS_Store
.vs/
.vscode/
.idea/

# Crash / build reports
sysinfo.txt
crashlytics-build.properties
*.apk
*.aab
*.unitypackage
*.app
```

- [ ] **Step 2: Create `docs/operations/` with a keeper**

Create `docs/operations/.gitkeep` with a single comment line:

```
# IL2CPP smoke logs live here (docs/operations/il2cpp-smoke-<date>.md).
```

- [ ] **Step 3: Verify the tree exists**

Run: `ls -la examples/peak-sdk-unity-reference/ docs/operations/`
Expected: `.gitignore` present under the example dir; `.gitkeep` under `docs/operations/`.

- [ ] **Step 4: Commit**

```bash
git add examples/peak-sdk-unity-reference/.gitignore docs/operations/.gitkeep
git commit -m "feat(examples): scaffold unity-reference dir + docs/operations (W3, #24)"
```

---

## Task 2: Local-feed plumbing — `NuGet.config`, `prepare-feed.sh`, `packages.config`, `manifest.json`

**Files:**
- Create: `examples/peak-sdk-unity-reference/NuGet.config`
- Create: `examples/peak-sdk-unity-reference/prepare-feed.sh`
- Create: `examples/peak-sdk-unity-reference/Assets/packages.config`
- Create: `examples/peak-sdk-unity-reference/Packages/manifest.json`

- [ ] **Step 1: `NuGet.config`** (LocalFeed for all `KyuzanInc.*`, nuget.org for the rest — D3/§5.1; note this diverges from the repo root, which routes Turnkey to GitHub)

Create `examples/peak-sdk-unity-reference/NuGet.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <!-- Local feed produced by prepare-feed.sh: BOTH KyuzanInc.Peak.Sdk (packed)
         and the vendored transitive KyuzanInc.Turnkey.Sdk live here, so the
         example needs no GitHub Packages auth at restore time. This intentionally
         differs from the repo-root nuget.config, which routes KyuzanInc.Turnkey.*
         to GitHub Packages. -->
    <add key="local-feed" value="./LocalFeed" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
    <!-- One pattern covering BOTH Peak and Turnkey, so the vendored Turnkey
         resolves locally (not from GitHub). -->
    <packageSource key="local-feed">
      <package pattern="KyuzanInc.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

- [ ] **Step 2: `prepare-feed.sh`** (pack the csproj + vendor the exact-pinned Turnkey nupkg — D3/§5.2)

Create `examples/peak-sdk-unity-reference/prepare-feed.sh`:

```bash
#!/usr/bin/env bash
# Populate ./LocalFeed so the Unity project can restore KyuzanInc.Peak.Sdk and its
# transitive KyuzanInc.Turnkey.Sdk WITHOUT GitHub Packages auth at Unity-restore time.
# Prerequisite: run `dotnet restore peak-sdk-csharp.sln` once first (warms the NuGet
# cache with the Turnkey nupkg). See README for the GitHub-auth alternative.
set -euo pipefail
cd "$(dirname "$0")"

rm -f ./LocalFeed/KyuzanInc.Peak.Sdk.*.nupkg   # avoid resolving a stale SDK build
mkdir -p ./LocalFeed

# 1) Pack the working-tree SDK (csproj, NOT the solution) into the local feed.
dotnet pack ../../packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj -c Release -o ./LocalFeed

# 2) Vendor the EXACT pinned transitive Turnkey nupkg from the NuGet cache.
#    The version is pinned exact in Directory.Packages.props as [X]; read it, or
#    hardcode it if this extraction ever drifts (the pin changes rarely).
TK_VER="$(grep -oE 'KyuzanInc\.Turnkey\.Sdk"[^/]*Version="\[?[0-9][^]"]*' ../../Directory.Packages.props | grep -oE '[0-9][^]"]*$')"
: "${TK_VER:?could not read KyuzanInc.Turnkey.Sdk version from Directory.Packages.props}"
TK_NUPKG="${NUGET_PACKAGES:-$HOME/.nuget/packages}/kyuzaninc.turnkey.sdk/${TK_VER}/kyuzaninc.turnkey.sdk.${TK_VER}.nupkg"
if [ ! -f "$TK_NUPKG" ]; then
  echo "ERROR: $TK_NUPKG not found." >&2
  echo "Run 'dotnet restore peak-sdk-csharp.sln' once (with GitHub Packages auth) to warm the cache, then re-run." >&2
  exit 1
fi
cp "$TK_NUPKG" ./LocalFeed/

echo "LocalFeed ready:"
ls -1 ./LocalFeed
```

- [ ] **Step 3: Make it executable**

Run: `chmod +x examples/peak-sdk-unity-reference/prepare-feed.sh`

- [ ] **Step 4: Derive the full closure from the lock file** (D8 — list every package NFU must restore, not just first-order)

Run: `dotnet list packages/peak-sdk-csharp/src/peak-sdk-csharp.csproj package --include-transitive --framework netstandard2.1`
(If that errors offline, read `packages/peak-sdk-csharp/src/packages.lock.json` and take every package under the `netstandard2.1` node with a resolved version.)
Expected: ~20 packages — the first-order six (KyuzanInc.Peak.Sdk, KyuzanInc.Turnkey.Sdk, System.Text.Json, Microsoft.Extensions.Http, Microsoft.Extensions.Logging.Abstractions, BouncyCastle.Cryptography) **plus** the second-order runtime packages (Microsoft.Extensions.Options, Microsoft.Extensions.Primitives, Microsoft.Extensions.Logging, Microsoft.Extensions.DependencyInjection(.Abstractions), Microsoft.Extensions.Configuration.Abstractions, System.Buffers, System.Memory, System.Numerics.Vectors, System.Text.Encodings.Web, System.Threading.Tasks.Extensions, Microsoft.Bcl.AsyncInterfaces, System.Diagnostics.DiagnosticSource, System.Runtime.CompilerServices.Unsafe, System.ComponentModel.Annotations). Use the **exact resolved versions** from the lock file (the netstandard2.1 node — the csproj multi-targets, so read that node specifically, not net8.0).

- [ ] **Step 5: `Assets/packages.config`** (the complete closure — fill versions from Step 4's lock-file output)

Create `examples/peak-sdk-unity-reference/Assets/packages.config`. Use NuGetForUnity's `packages.config` shape; `KyuzanInc.Peak.Sdk` is `manuallyInstalled="true"` (the rest are its dependencies). **Replace every `version=` below with the exact value from Step 4** (the lock file is the source of truth — do not guess):

```xml
<?xml version="1.0" encoding="utf-8"?>
<packages>
  <package id="KyuzanInc.Peak.Sdk" version="0.1.0-alpha.2" manuallyInstalled="true" />
  <package id="KyuzanInc.Turnkey.Sdk" version="0.1.0-alpha.0" />
  <package id="System.Text.Json" version="8.0.5" />
  <package id="Microsoft.Extensions.Http" version="8.0.1" />
  <package id="Microsoft.Extensions.Logging.Abstractions" version="8.0.2" />
  <package id="BouncyCastle.Cryptography" version="2.5.0" />
  <!-- Second-order runtime closure (NFU is NOT transitive — list all). Versions
       MUST come from packages/peak-sdk-csharp/src/packages.lock.json (Step 4). -->
  <package id="Microsoft.Extensions.Options" version="REPLACE_FROM_LOCKFILE" />
  <package id="Microsoft.Extensions.Primitives" version="REPLACE_FROM_LOCKFILE" />
  <package id="Microsoft.Extensions.Logging" version="REPLACE_FROM_LOCKFILE" />
  <package id="Microsoft.Extensions.DependencyInjection" version="REPLACE_FROM_LOCKFILE" />
  <package id="Microsoft.Extensions.DependencyInjection.Abstractions" version="REPLACE_FROM_LOCKFILE" />
  <package id="Microsoft.Extensions.Configuration.Abstractions" version="REPLACE_FROM_LOCKFILE" />
  <package id="Microsoft.Bcl.AsyncInterfaces" version="REPLACE_FROM_LOCKFILE" />
  <package id="System.Buffers" version="REPLACE_FROM_LOCKFILE" />
  <package id="System.Memory" version="REPLACE_FROM_LOCKFILE" />
  <package id="System.Numerics.Vectors" version="REPLACE_FROM_LOCKFILE" />
  <package id="System.Text.Encodings.Web" version="REPLACE_FROM_LOCKFILE" />
  <package id="System.Threading.Tasks.Extensions" version="REPLACE_FROM_LOCKFILE" />
  <package id="System.Diagnostics.DiagnosticSource" version="REPLACE_FROM_LOCKFILE" />
  <package id="System.Runtime.CompilerServices.Unsafe" version="REPLACE_FROM_LOCKFILE" />
  <package id="System.ComponentModel.Annotations" version="REPLACE_FROM_LOCKFILE" />
</packages>
```

> If Step 4 shows a package not listed here, ADD it. If it shows one of these is NOT in the netstandard2.1 closure, REMOVE it. The lock file wins. There must be **no `REPLACE_FROM_LOCKFILE` left** when this task is done.

- [ ] **Step 6: `Packages/manifest.json`** (NFU via git-URL UPM — D12; the exact tag is OQ2, default to a recent 4.x)

Create `examples/peak-sdk-unity-reference/Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.github-glitchenzo.nugetforunity": "https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity#v4.4.0",
    "com.unity.modules.imgui": "1.0.0",
    "com.unity.modules.jsonserialize": "1.0.0",
    "com.unity.modules.unitywebrequest": "1.0.0"
  }
}
```

> OQ2: confirm `v4.4.0` is a real NuGetForUnity tag the smoke-runner's Unity accepts; bump if needed. The git-URL form needs no scoped registry (deterministic open).

- [ ] **Step 7: Verify the feed builds** (requires GitHub Packages auth configured per `docs/development.md`, and one prior `dotnet restore`)

Run:
```bash
dotnet restore peak-sdk-csharp.sln --locked-mode
examples/peak-sdk-unity-reference/prepare-feed.sh
```
Expected: `LocalFeed ready:` followed by two files — `KyuzanInc.Peak.Sdk.0.1.0-alpha.2.nupkg` and `kyuzaninc.turnkey.sdk.0.1.0-alpha.0.nupkg` (names case may vary).

- [ ] **Step 8: Confirm the feed is gitignored** (D4 — nupkgs must NOT be staged)

Run: `git status --porcelain examples/peak-sdk-unity-reference/LocalFeed/`
Expected: **empty output** (LocalFeed is ignored).

- [ ] **Step 9: Commit**

```bash
git add examples/peak-sdk-unity-reference/NuGet.config \
        examples/peak-sdk-unity-reference/prepare-feed.sh \
        examples/peak-sdk-unity-reference/Assets/packages.config \
        examples/peak-sdk-unity-reference/Packages/manifest.json
git commit -m "feat(examples): local-feed plumbing + full packages.config closure (#24)"
```

---

## Task 3: Unity config — `link.xml`, `ProjectSettings` seeds, `ProjectVersion.txt`

**Files:**
- Create: `examples/peak-sdk-unity-reference/Assets/link.xml`
- Create: `examples/peak-sdk-unity-reference/ProjectSettings/ProjectVersion.txt`
- Create: `examples/peak-sdk-unity-reference/ProjectSettings/ProjectSettings.asset`
- Create: `examples/peak-sdk-unity-reference/ProjectSettings/EditorBuildSettings.asset`
- Create: `examples/peak-sdk-unity-reference/ProjectSettings/EditorSettings.asset`

> **D5 scope note (conscious amendment):** spec D5/§5.5 says "commit the **full standard `ProjectSettings/` set**." An agent cannot author all ~15 Unity-generated settings files faithfully without Unity. This task commits the **load-bearing seeds** — `ProjectVersion.txt`, `ProjectSettings.asset` (IL2CPP/apiCompat/stripping), `EditorBuildSettings.asset` (the scene), and `EditorSettings.asset` (Force-Text serialization, so the human's first save produces text-YAML diffs not binary) — and **Task 8 (human) opens Unity once to generate + commit the remaining standard set** (TagManager, DynamicsManager, GraphicsSettings, QualitySettings, InputManager, TimeManager, PresetManager, Physics2DSettings, etc.). This narrows D5's "commit the full set up front" to "commit the load-bearing seeds; the human completes the set on first open" — a deliberate division, not a silent reversal.

- [ ] **Step 1: `Assets/link.xml`** (preserve BouncyCastle + Turnkey under IL2CPP stripping — D11)

Create `examples/peak-sdk-unity-reference/Assets/link.xml`:

```xml
<!--
  IL2CPP managed-stripping preservation. BouncyCastle has a documented history of
  UnityLinker stripping failures; Turnkey/PeakCrypto depend on it. STJ uses
  source-gen (AOT-safe) so it likely needs no preservation, kept here defensively.
  Remove an entry ONLY if a Player build proves it unnecessary; record the result
  in docs/operations/il2cpp-smoke-<date>.md.
-->
<linker>
  <assembly fullname="BouncyCastle.Cryptography" preserve="all" />
  <assembly fullname="KyuzanInc.Turnkey.Sdk" preserve="all" />
  <assembly fullname="KyuzanInc.Peak.Sdk" preserve="all" />
  <assembly fullname="System.Text.Json" preserve="all" />
</linker>
```

- [ ] **Step 2: `ProjectSettings/ProjectVersion.txt`** (Unity pin — OQ1; default to a 6000.0 LTS, the smoke-runner confirms in Task 8)

Create `examples/peak-sdk-unity-reference/ProjectSettings/ProjectVersion.txt`:

```
m_EditorVersion: 6000.0.32f1
m_EditorVersionWithRevision: 6000.0.32f1 (b2e806cf271c)
```

> OQ1: replace `6000.0.32f1` with the exact 6000.0.x LTS installed on the smoke-runner's machine (Task 8 normalizes this). Any 6000.0.x is acceptable; the revision hash line is regenerated by Unity on open.

- [ ] **Step 3: `ProjectSettings/ProjectSettings.asset`** (seed with the load-bearing IL2CPP fields — D11/§5.5; Unity completes the rest on open)

Create `examples/peak-sdk-unity-reference/ProjectSettings/ProjectSettings.asset`:

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!129 &1
PlayerSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 26
  productName: PeakSdkUnityReference
  companyName: Kyuzan Inc.
  applicationIdentifier:
    Standalone: com.kyuzan.peak.example
  # --- Load-bearing for the IL2CPP smoke (spec D11/§5.5) ---
  # apiCompatibilityLevel: 6 == .NET Standard 2.1 (so the netstandard2.1 SDK asset
  # + System.Text.Json/Microsoft.Extensions.* resolve). scriptingBackend 1 == IL2CPP.
  # managedStrippingLevel 0 == Disabled/Minimal start point (raise + re-test in the smoke).
  apiCompatibilityLevelPerPlatform: {}
  apiCompatibilityLevel: 6
  scriptingBackend:
    Standalone: 1
  il2cppCompilerConfiguration: {}
  managedStrippingLevel:
    Standalone: 0
  allowUnsafeCode: 0
```

> This is a **seed**. Unity rewrites `ProjectSettings.asset` to its full schema on first open (Task 8); the agent only needs the IL2CPP/apiCompat/stripping intent captured. Do not hand-expand the rest.

- [ ] **Step 4: `ProjectSettings/EditorBuildSettings.asset`** (put SampleScene in the build list — else the IL2CPP Player build has no scene)

Create `examples/peak-sdk-unity-reference/ProjectSettings/EditorBuildSettings.asset`:

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1045 &1
EditorBuildSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Scenes:
  - enabled: 1
    path: Assets/Scenes/SampleScene.unity
    guid: feed00000000000000000000000000a3
  m_configObjects: {}
```

- [ ] **Step 5: `ProjectSettings/EditorSettings.asset`** (Force-Text serialization so Unity writes text-YAML diffs — D5 determinism)

Create `examples/peak-sdk-unity-reference/ProjectSettings/EditorSettings.asset`:

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!159 &1
EditorSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 12
  m_SerializationMode: 2
  m_LineEndingsForNewScripts: 1
  m_DefaultBehaviorMode: 0
  m_EnterPlayModeOptionsEnabled: 0
  m_EnterPlayModeOptions: 3
```

> `m_SerializationMode: 2` == Force Text (so scenes/assets serialize as readable YAML, not binary). Unity completes the rest of `EditorSettings.asset` on first open.

- [ ] **Step 6: Commit**

```bash
git add examples/peak-sdk-unity-reference/Assets/link.xml \
        examples/peak-sdk-unity-reference/ProjectSettings/ProjectVersion.txt \
        examples/peak-sdk-unity-reference/ProjectSettings/ProjectSettings.asset \
        examples/peak-sdk-unity-reference/ProjectSettings/EditorBuildSettings.asset \
        examples/peak-sdk-unity-reference/ProjectSettings/EditorSettings.asset
git commit -m "feat(examples): IL2CPP link.xml + ProjectSettings seeds (#24)"
```

---

## Task 4: The MonoBehaviour, asmdef, scene, and `.meta` files

**Files:**
- Create: `examples/peak-sdk-unity-reference/Assets/Scripts/PeakExampleDemo.cs`
- Create: `examples/peak-sdk-unity-reference/Assets/Scripts/Peak.Example.asmdef`
- Create: `examples/peak-sdk-unity-reference/Assets/Scenes/SampleScene.unity`
- Create: all `.meta` files (fixed GUIDs so the scene→script reference resolves)

- [ ] **Step 1: `PeakExampleDemo.cs`** (the full state machine — D2/D6/D7/D9; calls the verified §1.2 surface)

Create `examples/peak-sdk-unity-reference/Assets/Scripts/PeakExampleDemo.cs`:

```csharp
// Reference consumer of KyuzanInc.Peak.Sdk for the Unity IL2CPP AOT smoke.
// Modeled on the peak-monorepo PeakSdkDemo.cs (not vendored). No UniTask:
// async void MonoBehaviour callbacks with explicit try/catch (the one place
// async void is acceptable). See docs/superpowers/specs/
// 2026-06-09-unity-reference-example-otp-login-design.md.
using System;
using System.Threading.Tasks;
using KyuzanInc.Peak.Sdk;
using KyuzanInc.Peak.Sdk.Models;
using UnityEngine;

namespace Peak.Example
{
    public sealed class PeakExampleDemo : MonoBehaviour
    {
        // chainType valid domain is exactly {"evm","solana"} (spec D6); "ETHEREUM" throws.
        private const string ChainTypeEvm = "evm";
        private const string KeyFormatHex = "HEXADECIMAL";

        private enum State { Uninitialized, Idle, AwaitingOtp, Authenticated }

        // apiUrl is non-secret, so a SerializeField is fine. The projectApiKey is
        // NEVER serialized (it would land in the committed scene YAML) — runtime entry
        // only, with an env-var fallback for Standalone convenience (spec D9).
        [SerializeField] private string apiUrl = "https://api.peak.xyz";

        private PeakClient _client;
        private AuthenticatedPeakClient _authed;
        private State _state = State.Uninitialized;
        private bool _busy;

        private string _apiKey = "";
        private string _email = "";
        private string _otpCode = "";
        private string _pendingOtpId = "";
        private string _importKeyHex = "";   // TEST keys only
        private string _address = "";        // set from List Addresses; gates Export
        private string _log = "";

        private void Awake()
        {
            // Env-var fallback so a Standalone smoke can avoid typing the key each run.
            var fromEnv = Environment.GetEnvironmentVariable("PEAK_PROJECT_API_KEY");
            if (!string.IsNullOrEmpty(fromEnv)) _apiKey = fromEnv;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 460, 600));
            GUILayout.Label($"Peak SDK — Unity reference example   [{_state}]");
            if (_busy) GUILayout.Label("working…");

            switch (_state)
            {
                case State.Uninitialized: DrawUninitialized(); break;
                case State.Idle: DrawIdle(); break;
                case State.AwaitingOtp: DrawAwaitingOtp(); break;
                case State.Authenticated: DrawAuthenticated(); break;
            }

            GUILayout.Label("Log:");
            GUILayout.TextArea(_log, GUILayout.Height(180));
            GUILayout.EndArea();
        }

        private void DrawUninitialized()
        {
            GUILayout.Label("API URL:");
            apiUrl = GUILayout.TextField(apiUrl);
            GUILayout.Label("Project API key (TEST env — not stored in the scene):");
            _apiKey = GUILayout.PasswordField(_apiKey, '*');
            if (GUILayout.Button("Initialize"))
            {
                try
                {
                    // The (apiUrl, projectApiKey) overload leaves LoggerFactory null
                    // -> NullLogger, so the SDK's Debug request-body logging is inert (D9).
                    _client = PeakClient.Initialize(apiUrl, _apiKey);
                    _state = State.Idle;
                    Append("initialized");
                }
                catch (PeakError ex) { Fail(ex); }
            }
        }

        private void DrawIdle()
        {
            GUILayout.Label("Email:");
            _email = GUILayout.TextField(_email);
            if (Button("Send OTP")) OnSendOtp();
        }

        private void DrawAwaitingOtp()
        {
            GUILayout.Label("OTP code:");
            _otpCode = GUILayout.TextField(_otpCode);
            if (Button("Complete Login")) OnCompleteLogin();
        }

        private void DrawAuthenticated()
        {
            if (Button("List Accounts")) OnListAccounts();
            if (Button("List Addresses (uses first account)")) OnListAddresses();
            GUILayout.Label("Private key to import (TEST only):");
            _importKeyHex = GUILayout.TextField(_importKeyHex);
            if (Button("Import Private Key")) OnImport();
            // Export needs an address (spec §4.3 ordering precondition).
            GUI.enabled = !string.IsNullOrEmpty(_address) && !_busy;
            if (GUILayout.Button("Export Private Key")) OnExport();
            GUI.enabled = !_busy;
            if (Button("Logout")) { _client.Logout(); _authed = null; _state = State.Idle; Append("logged out"); }
        }

        // --- async void handlers: try/catch, no ConfigureAwait(false), _busy guard (D7) ---

        private async void OnSendOtp()
        {
            if (!Begin()) return;
            try
            {
                InitOtpLoginResponse? result = await _client.InitOtpLoginAsync(_email);
                _pendingOtpId = result?.OtpId ?? "";
                if (string.IsNullOrEmpty(_pendingOtpId)) { Append("no otpId returned"); return; }
                _state = State.AwaitingOtp;
                Append("OTP sent");
            }
            catch (PeakError ex) { Fail(ex); }
            catch (Exception ex) { Fail(ex); }
            finally { End(); }
        }

        private async void OnCompleteLogin()
        {
            if (!Begin()) return;
            try
            {
                await _client.CompleteOtpLoginAsync(_email, _pendingOtpId, _otpCode);
                _authed = _client.Authenticate();
                _state = State.Authenticated;
                Append("authenticated");
            }
            catch (PeakError ex) { Fail(ex); }
            catch (Exception ex) { Fail(ex); }
            finally { End(); }
        }

        private async void OnListAccounts()
        {
            if (!Begin()) return;
            try
            {
                AccountResponse[] accounts = await _authed.ListAccountsAsync();
                Append($"accounts: {accounts.Length}");
                foreach (var a in accounts) Append($"  acct {Redact(a.Id)} idx={a.AccountIndex}");
            }
            catch (PeakError ex) { Fail(ex); }
            catch (Exception ex) { Fail(ex); }
            finally { End(); }
        }

        private async void OnListAddresses()
        {
            if (!Begin()) return;
            try
            {
                AccountResponse[] accounts = await _authed.ListAccountsAsync();
                if (accounts.Length == 0) { Append("no accounts"); return; }
                AccountAddressResponse[] addrs = await _authed.ListAccountAddressesAsync(accounts[0].Id ?? "");
                Append($"addresses: {addrs.Length}");
                if (addrs.Length > 0) { _address = addrs[0].Address ?? ""; Append($"  using {Redact(_address)}"); }
            }
            catch (PeakError ex) { Fail(ex); }
            catch (Exception ex) { Fail(ex); }
            finally { End(); }
        }

        private async void OnImport()
        {
            if (!Begin()) return;
            try
            {
                InitImportPrivateKeyResult init = await _authed.InitImportPrivateKeyAsync();
                string bundle = PeakCrypto.EncryptPrivateKeyToBundle(new PeakCrypto.EncryptPrivateKeyToBundleParams
                {
                    PrivateKey = _importKeyHex,
                    ImportBundle = init.ImportBundle,
                    OrganizationId = init.OrganizationId,
                    UserId = init.UserId,
                    KeyFormat = KeyFormatHex,
                });
                await _authed.CompleteImportPrivateKeyAsync(bundle, ChainTypeEvm);
                Append("import ok");
            }
            catch (PeakError ex) { Fail(ex); }
            catch (Exception ex) { Fail(ex); }
            finally { End(); }
        }

        private async void OnExport()
        {
            if (!Begin()) return;
            try
            {
                PeakCrypto.KeyPair target = PeakCrypto.GenerateP256KeyPair();
                ExportPrivateKeyResult exp = await _authed.ExportPrivateKeyAsync(_address, target.PublicKey);
                string key = PeakCrypto.DecryptExportBundle(new PeakCrypto.DecryptExportBundleParams
                {
                    ExportBundle = exp.ExportBundle,
                    EmbeddedKey = target.PrivateKey,
                    OrganizationId = exp.OrganizationId,
                    KeyFormat = KeyFormatHex,
                    ReturnMnemonic = false,
                });
                // NEVER log the raw key (spec D9) — only a redacted marker.
                Append($"export ok: {Redact(key)}");
            }
            catch (PeakError ex) { Fail(ex); }
            catch (Exception ex) { Fail(ex); }
            finally { End(); }
        }

        // --- helpers ---
        private bool Button(string label) => GUILayout.Button(label) && !_busy;
        private bool Begin() { if (_busy) return false; _busy = true; return true; }
        private void End() { _busy = false; }
        private void Append(string line) => _log = line + "\n" + _log;
        private void Fail(Exception ex)
        {
            string code = ex is PeakError pe ? pe.Code : "ERR";
            // Log code + message only; never ApiResponse.RawResponseBody (may echo secrets, D9).
            Append($"{code}: {ex.Message}");
            Debug.LogError($"[PeakExample] {code}: {ex.Message}");
        }
        private static string Redact(string s)
            => string.IsNullOrEmpty(s) ? "(empty)" : (s.Length <= 6 ? "…" : s.Substring(0, 6) + "…(redacted)");
    }
}
```

- [ ] **Step 2: `Peak.Example.asmdef`** (asmdef; relies on NFU DLLs' Auto-Reference — §5.4)

Create `examples/peak-sdk-unity-reference/Assets/Scripts/Peak.Example.asmdef`:

```json
{
    "name": "Peak.Example",
    "rootNamespace": "Peak.Example",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

> If, during the Task-8 smoke, the NFU-restored DLLs do not auto-reference, set `"overrideReferences": true` and list the DLL names in `"precompiledReferences"` (e.g. `"KyuzanInc.Peak.Sdk.dll"`, `"KyuzanInc.Turnkey.Sdk.dll"`, `"System.Text.Json.dll"`). Record which was needed in the smoke log.

- [ ] **Step 3: `.meta` files** (fixed GUIDs so the scene resolves the script)

Create `examples/peak-sdk-unity-reference/Assets/Scripts.meta`:

```
fileFormatVersion: 2
guid: feed00000000000000000000000000b1
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

Create `examples/peak-sdk-unity-reference/Assets/Scenes.meta`:

```
fileFormatVersion: 2
guid: feed00000000000000000000000000b2
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

Create `examples/peak-sdk-unity-reference/Assets/Scripts/PeakExampleDemo.cs.meta`:

```
fileFormatVersion: 2
guid: feed00000000000000000000000000a1
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
```

Create `examples/peak-sdk-unity-reference/Assets/Scripts/Peak.Example.asmdef.meta`:

```
fileFormatVersion: 2
guid: feed00000000000000000000000000a2
AssemblyDefinitionImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

Create `examples/peak-sdk-unity-reference/Assets/Scenes/SampleScene.unity.meta`:

```
fileFormatVersion: 2
guid: feed00000000000000000000000000a3
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

Create `examples/peak-sdk-unity-reference/Assets/link.xml.meta`:

```
fileFormatVersion: 2
guid: feed00000000000000000000000000a4
TextScriptImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

Create `examples/peak-sdk-unity-reference/Assets/packages.config.meta`:

```
fileFormatVersion: 2
guid: feed00000000000000000000000000a5
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

- [ ] **Step 4: `Assets/Scenes/SampleScene.unity`** (seed scene — one GameObject carrying the MonoBehaviour via the script GUID; NO api key serialized)

Create `examples/peak-sdk-unity-reference/Assets/Scenes/SampleScene.unity`:

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!29 &1
OcclusionCullingSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_OcclusionBakeSettings:
    smallestOccluder: 5
    smallestHole: 0.25
    backfaceThreshold: 100
  m_SceneGUID: 00000000000000000000000000000000
  m_OcclusionCullingData: {fileID: 0}
--- !u!104 &2
RenderSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 9
  m_Fog: 0
  m_FogColor: {r: 0.5, g: 0.5, b: 0.5, a: 1}
  m_FogMode: 3
  m_FogDensity: 0.01
  m_LinearFogStart: 0
  m_LinearFogEnd: 300
  m_AmbientSkyColor: {r: 0.212, g: 0.227, b: 0.259, a: 1}
  m_AmbientEquatorColor: {r: 0.114, g: 0.125, b: 0.133, a: 1}
  m_AmbientGroundColor: {r: 0.047, g: 0.043, b: 0.035, a: 1}
  m_AmbientIntensity: 1
  m_AmbientMode: 0
  m_SkyboxMaterial: {fileID: 0}
  m_HaloStrength: 0.5
  m_FlareStrength: 1
  m_FlareFadeSpeed: 3
  m_HaloTexture: {fileID: 0}
  m_SpotCookie: {fileID: 10001, guid: 0000000000000000e000000000000000, type: 0}
  m_DefaultReflectionMode: 0
  m_DefaultReflectionResolution: 128
  m_ReflectionBounces: 1
  m_ReflectionIntensity: 1
  m_CustomReflection: {fileID: 0}
  m_Sun: {fileID: 0}
  m_IndirectSpecularColor: {r: 0, g: 0, b: 0, a: 1}
  m_UseRadianceAmbientProbe: 0
--- !u!157 &3
LightmapSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 12
  m_GIWorkflowMode: 1
  m_GISettings:
    serializedVersion: 2
    m_BounceScale: 1
    m_IndirectOutputScale: 1
    m_AlbedoBoost: 1
    m_EnvironmentLightingMode: 0
    m_EnableBakedLightmaps: 0
    m_EnableRealtimeLightmaps: 0
  m_LightmapEditorSettings:
    serializedVersion: 12
    m_Resolution: 2
    m_BakeResolution: 40
    m_AtlasSize: 1024
    m_AO: 0
    m_AOMaxDistance: 1
    m_CompAOExponent: 1
    m_CompAOExponentDirect: 0
    m_ExtractAmbientOcclusion: 0
    m_Padding: 2
    m_LightmapParameters: {fileID: 0}
    m_LightmapsBakeMode: 1
    m_TextureCompression: 1
    m_FinalGather: 0
    m_FinalGatherFiltering: 1
    m_FinalGatherRayCount: 256
    m_ReflectionCompression: 2
    m_MixedBakeMode: 2
    m_BakeBackend: 1
    m_PVRSampling: 1
    m_PVRDirectSampleCount: 32
    m_PVRSampleCount: 512
    m_PVRBounces: 2
    m_PVRFilteringMode: 1
    m_PVRCulling: 1
    m_LightProbeSampleCountMultiplier: 4
  m_LightingDataAsset: {fileID: 0}
  m_LightingSettings: {fileID: 0}
--- !u!196 &4
NavMeshSettings:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_BuildSettings:
    serializedVersion: 3
    agentTypeID: 0
    agentRadius: 0.5
    agentHeight: 2
    agentSlope: 45
    agentClimb: 0.4
    ledgeDropHeight: 0
    maxJumpAcrossDistance: 0
    minRegionArea: 2
    manualCellSize: 0
    cellSize: 0.16666667
    manualTileSize: 0
    tileSize: 256
    buildHeightMesh: 0
    maxJobWorkers: 0
    preserveTilesOutsideBounds: 0
    debug:
      m_Flags: 0
  m_NavMeshData: {fileID: 0}
--- !u!1 &1000000000
GameObject:
  m_ObjectHideFlags: 0
  serializedVersion: 6
  m_Component:
  - component: {fileID: 1000000001}
  - component: {fileID: 1000000002}
  m_Layer: 0
  m_Name: PeakExample
  m_TagString: Untagged
  m_IsActive: 1
--- !u!4 &1000000001
Transform:
  m_ObjectHideFlags: 0
  m_GameObject: {fileID: 1000000000}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children: []
  m_Father: {fileID: 0}
--- !u!114 &1000000002
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_GameObject: {fileID: 1000000000}
  m_Enabled: 1
  m_Script: {fileID: 11500000, guid: feed00000000000000000000000000a1, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  apiUrl: https://api.peak.xyz
```

> Seed scene: it includes the four standard scene-settings objects (`OcclusionCullingSettings`/`RenderSettings`/`LightmapSettings`/`NavMeshSettings`) so it opens cleanly in Unity 6000.0 (a GameObject-only scene can warn / show black lighting until first save). It has **no `projectApiKey` field serialized** (the script doesn't declare one — D9). Unity may silently upgrade a serializedVersion on first open and rewrite whitespace; Task 8 re-saves to normalize. The MonoBehaviour `m_Script` GUID `…a1` matches `PeakExampleDemo.cs.meta`.

- [ ] **Step 5: Confirm the scene is key-free** (D9/R9 guard)

Run: `grep -i "apikey\|projectapikey\|secret" examples/peak-sdk-unity-reference/Assets/Scenes/SampleScene.unity || echo "scene is key-free"`
Expected: `scene is key-free`.

- [ ] **Step 6: Commit**

```bash
git add examples/peak-sdk-unity-reference/Assets/
git commit -m "feat(examples): IMGUI MonoBehaviour + asmdef + seed scene + meta (#24)"
```

---

## Task 5: API-conformance test (D13 — the one in-repo automated guard)

**Files:**
- Create: `packages/peak-sdk-csharp/tests/ExampleApiConformanceTests.cs`
- Test: same file (it IS the test)

This compiles the example's exact SDK-call expressions against the SDK's public surface in the existing test project (which already references the SDK and runs in CI via `dotnet test`). It catches signature/nullability/params-shape drift mechanically — the class of bug a human review can miss. It does **not** execute (no network/auth); the value is the compile + the `chainType` constant assertion.

> Note: the test project (`packages/peak-sdk-csharp/tests/peak-sdk-csharp.Tests.csproj`) sets `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>` (overriding the repo-root `true`), so a stray nullable warning would not fail the build. The code below is nevertheless written warning-clean (nullable locals, `?? ""`) so a maintainer copying these patterns into the warnings-as-error SDK `src` project is not bitten. A **signature** mismatch (wrong method/param/type) is still a hard compile error here regardless of the warning setting — that is the guard.

- [ ] **Step 1: Write the conformance test**

Create `packages/peak-sdk-csharp/tests/ExampleApiConformanceTests.cs`:

```csharp
// Compile-only API-conformance guard for examples/peak-sdk-unity-reference/.
// Mirrors the SDK calls PeakExampleDemo.cs makes (minus UnityEngine). If the SDK's
// public surface drifts, this fails to COMPILE in the existing test build — the
// automated floor the design (D13) requires. The CompileOnly method is never run
// (it would need network/auth); only its type-checking matters.
using System.Threading.Tasks;
using KyuzanInc.Peak.Sdk;
using KyuzanInc.Peak.Sdk.Models;
using Xunit;

namespace KyuzanInc.Peak.Sdk.Tests
{
    public sealed class ExampleApiConformanceTests
    {
        private const string ChainTypeEvm = "evm";
        private const string KeyFormatHex = "HEXADECIMAL";

        [Fact]
        public void Example_chainType_constant_is_the_supported_value()
        {
            // The example uses "evm"; the SDK accepts exactly {"evm","solana"} and
            // throws on anything else (e.g. "ETHEREUM"). Pin the value here.
            Assert.Equal("evm", ChainTypeEvm);
        }

        [Fact]
        public void Example_sdk_calls_type_check_against_the_public_surface()
        {
            // The proof is that CompileOnly compiles. Reference it without running it
            // (false is not a compile-time constant, so no unreachable-code warning).
            if (bool.Parse(bool.FalseString))
            {
                _ = CompileOnly(null!, null!);
            }
            Assert.True(true);
        }

        private static async Task CompileOnly(PeakClient client, AuthenticatedPeakClient authed)
        {
            // Model props are nullable (string?); keep locals null-clean so the file
            // is warning-free even though the test csproj relaxes warnings-as-errors.
            InitOtpLoginResponse? otp = await client.InitOtpLoginAsync("e@example.com");
            string otpId = otp?.OtpId ?? "";
            CompleteOtpLoginResult login = await client.CompleteOtpLoginAsync("e@example.com", otpId, "000000");
            _ = login.SessionJwt;
            AuthenticatedPeakClient a = client.Authenticate();
            client.Logout();

            AccountResponse[] accounts = await authed.ListAccountsAsync();
            string accountId = accounts.Length > 0 ? accounts[0].Id ?? "id" : "id";
            AccountAddressResponse[] addrs = await authed.ListAccountAddressesAsync(accountId);
            string address = addrs.Length > 0 ? addrs[0].Address ?? "addr" : "addr";

            InitImportPrivateKeyResult init = await authed.InitImportPrivateKeyAsync();
            string bundle = PeakCrypto.EncryptPrivateKeyToBundle(new PeakCrypto.EncryptPrivateKeyToBundleParams
            {
                PrivateKey = "deadbeef",
                ImportBundle = init.ImportBundle,
                OrganizationId = init.OrganizationId,
                UserId = init.UserId,
                KeyFormat = KeyFormatHex,
            });
            CompleteImportPrivateKeyResult imp = await authed.CompleteImportPrivateKeyAsync(bundle, ChainTypeEvm);
            _ = imp.Account;

            PeakCrypto.KeyPair target = PeakCrypto.GenerateP256KeyPair();
            ExportPrivateKeyResult exp = await authed.ExportPrivateKeyAsync(address, target.PublicKey);
            string key = PeakCrypto.DecryptExportBundle(new PeakCrypto.DecryptExportBundleParams
            {
                ExportBundle = exp.ExportBundle,
                EmbeddedKey = target.PrivateKey,
                OrganizationId = exp.OrganizationId,
                KeyFormat = KeyFormatHex,
                ReturnMnemonic = false,
            });
            _ = (a, address, key);
        }
    }
}
```

- [ ] **Step 2: Run the conformance test — expect PASS** (the compile is the test)

Run: `dotnet test peak-sdk-csharp.sln -c Release --filter "FullyQualifiedName~ExampleApiConformanceTests"`
Expected: PASS (2 tests). A compile error here means the example's SDK calls do **not** match the surface — fix the example (or the test) to the real API; do not loosen the assertion.

- [ ] **Step 3: Confirm the whole suite still passes + format clean**

Run:
```bash
dotnet test peak-sdk-csharp.sln -c Release --filter "Category!=E2E"
dotnet format peak-sdk-csharp.sln --verify-no-changes
```
Expected: all green; format reports no changes.

- [ ] **Step 4: Commit**

```bash
git add packages/peak-sdk-csharp/tests/ExampleApiConformanceTests.cs
git commit -m "test: API-conformance guard for the unity-reference example (D13, #24)"
```

---

## Task 6: Docs, plan, and `CLAUDE.md` edits (spec §5.7, exact strings)

**Files:**
- Modify: `CLAUDE.md`
- Modify: `plans/plans-peak-sdk-csharp.md`
- Modify: `docs/development.md`
- Modify: `README.md`

- [ ] **Step 1: `CLAUDE.md` — scope the UnityEngine rule + permit `examples/**`** (D10)

In `CLAUDE.md`, under "## Working with this repo", replace this exact bullet:

```
- `UnityEngine` outside `packages/peak-sdk-csharp-unity/`
```

with:

```
- `UnityEngine` in any **shipped** SDK package (everything under `packages/`).
  The Unity adapter `packages/peak-sdk-csharp-unity/` is the only package that
  may. Consumer **samples** under `examples/**` (e.g.
  `examples/peak-sdk-unity-reference/`) are NOT shipped packages and are not in
  `peak-sdk-csharp.sln`; they may reference `UnityEngine`. The dependency arrow
  stays example→SDK — no SDK package may reference an example.
```

- [ ] **Step 2: `plans/plans-peak-sdk-csharp.md` — un-stale the W3 row** (it points at a shipped milestone)

In `plans/plans-peak-sdk-csharp.md`, replace this exact W3 status-table row:

```
| W3  | PR 3: Godot + console smoke examples + Unity reference example via local file feed | ⬜ Deferred to v0.1.0-alpha.1 | scope decision | — | — |
```

with:

```
| W3  | PR 3: Godot + console smoke examples + Unity reference example via local file feed | 🟡 Unity portion delivered (Console + Godot still deferred) | `examples/peak-sdk-unity-reference/` + IL2CPP smoke log; design `docs/superpowers/specs/2026-06-09-unity-reference-example-otp-login-design.md`. (W3 had been deferred to v0.1.0-alpha.1, which shipped with .2 without it — the Unity third lands here.) | #24 | — |
```

- [ ] **Step 3: `plans/plans-peak-sdk-csharp.md` — fix the PR-3 "minimal" narrative** (D2 widened it)

In the PR-3 section, replace this exact sentence:

```
The Unity sample is intentionally **minimal**: just the asmdef + a
single MonoBehaviour that calls `PeakClient.Initialize`.
```

with:

```
The Unity sample runs the **full** OTP + wallet flow (init → OTP →
authenticate → list accounts → import/export), not just
`PeakClient.Initialize`: a bare init call would not exercise the HTTP +
System.Text.Json source-gen + async-state-machine + BouncyCastle paths that
IL2CPP AOT stripping actually breaks, which is the whole point of the smoke
(design D2). The in-repo sample now realizes the C-E2 / OQ3 IL2CPP smoke that
was previously delegated to the external `peak-sdk-unity` adapter.
```

- [ ] **Step 4: `docs/development.md` — add the example pointer + no-auth path**

In `docs/development.md`, after the "### Local `.nupkg` feed (offline or unreleased versions)" subsection (the paragraph ending "follow the same pattern after the `.nupkg` is copied into the Unity project's Packages folder."), append:

```markdown

### Unity reference example

`examples/peak-sdk-unity-reference/` is a minimal Unity 6000.0.x project that
consumes `KyuzanInc.Peak.Sdk` from a project-local `.nupkg` feed and runs the OTP
login + wallet flow as a human IL2CPP AOT smoke. Its `prepare-feed.sh` packs the
SDK **and vendors the transitive `KyuzanInc.Turnkey.Sdk` `.nupkg`** into the
example's `LocalFeed/`, so the Unity restore needs **no GitHub Packages auth** (a
deliberate divergence from the "still need the GitHub Packages source" guidance
above, which remains correct for a plain downstream consumer). A prior
`dotnet restore` of the solution must have warmed the cache first. The exact Unity
6000.0.x patch and the NuGetForUnity tag are confirmed by the smoke runner; see
the example's `README.md`. IL2CPP results are logged in
`docs/operations/il2cpp-smoke-<date>.md`.
```

- [ ] **Step 5: `README.md` (repo root) — one-line pointer**

In the repo-root `README.md`, find the section that lists docs/usage pointers (e.g. the "Development" or "Examples"/"Repository layout" area). Add this line in the most appropriate list (if no list fits, add a short "## Examples" section near the end):

```markdown
- [`examples/peak-sdk-unity-reference/`](examples/peak-sdk-unity-reference/README.md) — Unity 6000.0.x reference consumer + IL2CPP smoke (OTP login + wallet flow).
```

- [ ] **Step 6: Verify the doc edits and link integrity**

Run: `grep -rn "peak-sdk-unity-reference" CLAUDE.md plans/plans-peak-sdk-csharp.md docs/development.md README.md`
Expected: each file shows the new reference; no leftover "intentionally **minimal**" sentence in the plan (`grep -n "intentionally \*\*minimal\*\*" plans/plans-peak-sdk-csharp.md` returns nothing).

- [ ] **Step 7: Commit**

```bash
git add CLAUDE.md plans/plans-peak-sdk-csharp.md docs/development.md README.md
git commit -m "docs: wire unity-reference example into CLAUDE.md/plan/dev docs (#24)"
```

---

## Task 7: Example `README.md` + IL2CPP smoke-log template

**Files:**
- Create: `examples/peak-sdk-unity-reference/README.md`
- Create: `docs/operations/il2cpp-smoke-2026-06-09.md`

- [ ] **Step 1: Example `README.md`** (setup, no-auth default + GitHub-auth alt, test-creds warning, run + smoke)

Create `examples/peak-sdk-unity-reference/README.md`:

```markdown
# peak-sdk-unity-reference

A minimal Unity 6000.0.x project that consumes **KyuzanInc.Peak.Sdk** from a local
`.nupkg` feed and demonstrates the full OTP login + wallet import/export flow
through an IMGUI UI. Its purpose is an **IL2CPP AOT smoke**: prove that
`HttpClient` + System.Text.Json source-gen + `async/await` + BouncyCastle survive
IL2CPP stripping in a real Player build.

> ⚠️ Use a **test** Peak environment, a **test** `projectApiKey`, and **test**
> private keys only. The API key is entered at runtime and is never stored in the
> scene. Never paste a production key or a real wallet's private key.

## 1. Prepare the local feed (no GitHub Packages auth needed)

From the repo root, warm the NuGet cache once (this is the only step that needs
GitHub Packages auth — set it up per `docs/development.md`):

```bash
dotnet restore peak-sdk-csharp.sln --locked-mode
```

Then build the example's local feed:

```bash
examples/peak-sdk-unity-reference/prepare-feed.sh
```

This packs `KyuzanInc.Peak.Sdk` and copies the transitive `KyuzanInc.Turnkey.Sdk`
`.nupkg` into `LocalFeed/`, so the Unity restore itself needs no auth.

**Alternative (you already have GitHub Packages auth):** instead of the vendored
Turnkey nupkg, add the `github-kyuzan` source to this project's `NuGet.config`
and let NuGetForUnity pull Turnkey from GitHub Packages (mirrors the repo's
`consumer-restore-check` CI job).

## 2. Open in Unity

Open `examples/peak-sdk-unity-reference/` in Unity 6000.0.x. NuGetForUnity (pinned
in `Packages/manifest.json`) restores the packages listed in
`Assets/packages.config` into `Assets/Packages/`. Open `Assets/Scenes/SampleScene.unity`.

## 3. Run the flow (Editor Play mode)

Press Play. In the IMGUI panel: enter the API URL + test `projectApiKey` →
**Initialize** → enter email → **Send OTP** → enter the OTP code → **Complete
Login** → **List Accounts** / **List Addresses** / **Import** / **Export**.

## 4. IL2CPP Player smoke (the point)

Switch the platform to Standalone, set Scripting Backend = IL2CPP, build, and run
the same flow on the Player. Record the result (and any `link.xml` / stripping-level
finding) in `docs/operations/il2cpp-smoke-<date>.md`.

## Files

- `Assets/Scripts/PeakExampleDemo.cs` — the MonoBehaviour (IMGUI + `async void`).
- `Assets/packages.config` — the full NuGet dependency closure (NuGetForUnity is not transitive).
- `Assets/link.xml` — IL2CPP preservation for BouncyCastle + Turnkey.
- `NuGet.config` / `prepare-feed.sh` — the local-feed setup.
```

- [ ] **Step 2: IL2CPP smoke-log template** (link.xml pre-seeded — §5.6)

Create `docs/operations/il2cpp-smoke-2026-06-09.md`:

```markdown
# IL2CPP smoke — peak-sdk-unity-reference

- **Date:** 2026-06-09 (fill the actual run date)
- **Runner:** (name)
- **Unity version:** 6000.0.____ (from ProjectSettings/ProjectVersion.txt)
- **NuGetForUnity:** v____ (from Packages/manifest.json)
- **SDK:** KyuzanInc.Peak.Sdk 0.1.0-alpha.2 (local feed) + KyuzanInc.Turnkey.Sdk 0.1.0-alpha.0 (vendored)

## Configuration
- Scripting backend: IL2CPP
- Api Compatibility Level: .NET Standard 2.1
- Managed stripping level: ____ (Disabled/Minimal/Low/Medium/High — record what was tested)
- Target platform(s): Standalone (mac/win/linux) [; iOS / Android if toolchains available]
- link.xml present: yes (Assets/link.xml — preserves BouncyCastle, Turnkey, Peak.Sdk, System.Text.Json)

## Results (per step)
| Step | Editor Play | IL2CPP Player | Notes |
|---|---|---|---|
| Open project (zero errors) | ☐ | n/a | |
| NuGetForUnity restore (Assets/Packages populated) | ☐ | n/a | |
| Initialize | ☐ | ☐ | |
| Send OTP (InitOtpLoginAsync) | ☐ | ☐ | HttpClient + STJ |
| Complete Login (CompleteOtpLoginAsync) + Authenticate | ☐ | ☐ | async state machine |
| List Accounts / Addresses | ☐ | ☐ | STJ source-gen deserialize |
| Import (PeakCrypto.Encrypt + CompleteImport, chainType="evm") | ☐ | ☐ | BouncyCastle under AOT |
| Export (GenerateP256KeyPair + Export + DecryptExportBundle) | ☐ | ☐ | BouncyCastle under AOT |
| Post-await main-thread id == captured main-thread id | ☐ | ☐ | ConfigureAwait/SynchronizationContext (D7) |

## link.xml / stripping findings
- (Did any assembly need preserving that the seed link.xml missed? Could any entry be removed? Did a stripping level break BouncyCastle? Record here — a stripping failure is a SUCCESSFUL finding.)

## Outcome
- PASS / FAIL / PARTIAL — (summary)
```

- [ ] **Step 3: Commit**

```bash
git add examples/peak-sdk-unity-reference/README.md docs/operations/il2cpp-smoke-2026-06-09.md
git commit -m "docs(examples): README + IL2CPP smoke-log template (#24)"
```

---

## Task 8: Final verification + human IL2CPP-smoke handoff

**Files:** none created; verification + the human checklist.

- [ ] **Step 1: Solution is unaffected** (the example is not in the `.sln`)

Run:
```bash
dotnet restore peak-sdk-csharp.sln --locked-mode
dotnet build peak-sdk-csharp.sln -c Release
dotnet test peak-sdk-csharp.sln -c Release --filter "Category!=E2E"
dotnet format peak-sdk-csharp.sln --verify-no-changes
```
Expected: all green (incl. `ExampleApiConformanceTests`); format reports no changes; lock files unchanged (`git status` clean for `**/packages.lock.json`).

- [ ] **Step 2: The local feed still builds and is ignored**

Run:
```bash
examples/peak-sdk-unity-reference/prepare-feed.sh
git status --porcelain examples/peak-sdk-unity-reference/LocalFeed/
```
Expected: feed lists both nupkgs; `git status` for `LocalFeed/` is empty (ignored).

- [ ] **Step 3: No secrets / no `packages.config` placeholders staged**

Run:
```bash
grep -RIni "projectapikey\|ghp_\|api_key.*=.*['\"][A-Za-z0-9]" examples/peak-sdk-unity-reference/Assets/Scenes/ || echo "scene clean"
grep -n "REPLACE_FROM_LOCKFILE" examples/peak-sdk-unity-reference/Assets/packages.config || echo "packages.config complete"
```
Expected: `scene clean` and `packages.config complete` (no placeholders, no key in the scene).

- [ ] **Step 4: HUMAN — open in Unity, normalize, smoke** (the Unity-dependent acceptance criteria; spec §6 layers 5–6)

This step needs a machine with Unity 6000.0.x; it cannot run in CI or headless.

1. Run `examples/peak-sdk-unity-reference/prepare-feed.sh`.
2. Open `examples/peak-sdk-unity-reference/` in Unity. Confirm **zero console errors** and that NuGetForUnity restored `Assets/Packages/`. If the asmdef does not see the SDK, set `overrideReferences`/`precompiledReferences` (Task 4 Step 2 note) and record it.
   - **Verify Player Settings → Api Compatibility Level == `.NET Standard 2.1`** in the Inspector. The seeded `ProjectSettings.asset` uses `apiCompatibilityLevel: 6`, which is a best-guess enum value and may resolve to `.NET Standard 2.0`; a wrong value silently breaks the netstandard2.1 SDK + System.Text.Json resolution at runtime. Fix it in the Inspector if needed and commit the corrected `ProjectSettings.asset`. Confirm Scripting Backend == IL2CPP and record the Managed Stripping Level.
3. Open `Assets/Scenes/SampleScene.unity`. Let Unity normalize `ProjectSettings/*` and the scene; **verify the saved scene still has no API key** (`git diff` the `.unity` file — only `apiUrl` should be serialized).
4. Press Play; run init → OTP → authenticate → list → import → export against a **test** Peak environment (URL + test `projectApiKey` + test keys — OQ6).
5. Switch to Standalone, Scripting Backend = IL2CPP, build, run the same flow on the Player.
6. Fill `docs/operations/il2cpp-smoke-2026-06-09.md` (rename to the real date) with pass/fail per step, the stripping level used, and any link.xml finding.
7. Commit the Unity-normalized `ProjectSettings/*`, the normalized scene, and the completed smoke log:
   ```bash
   git add examples/peak-sdk-unity-reference/ProjectSettings/ \
           examples/peak-sdk-unity-reference/Assets/Scenes/SampleScene.unity \
           docs/operations/il2cpp-smoke-*.md
   git commit -m "test(examples): IL2CPP smoke results + Unity-normalized project (#24)"
   ```

- [ ] **Step 5: Final commit (if any verification fixups were needed)**

```bash
git add -A examples/ docs/ packages/peak-sdk-csharp/tests/
git commit -m "chore(examples): finalize unity-reference example (#24)" || echo "nothing to finalize"
```

---

## Self-Review (run after the plan is written)

- **Spec coverage:** D1 (local feed → Task 2). D2 (full flow → Task 4 script). D3 (vendor Turnkey, exact pin → Task 2 prepare-feed.sh). D4 (don't commit nupkg → Task 1 .gitignore + Task 2 Step 8). D5 (committed skeleton + Unity.gitignore base → Tasks 1/3/4 + Task 8 normalize). D6 (chainType "evm" + normative domain → Task 4 const + Task 5 assertion). D7 (async void/try-catch/no-ConfigureAwait/_busy → Task 4 handlers). D8 (full packages.config closure from lock file → Task 2 Steps 4–5). D9 (no SerializeField key, NullLogger, redacted logging, key-free scene → Task 4 + Task 8 Step 3). D10 (CLAUDE.md carve-out → Task 6 Step 1). D11 (link.xml + stripping level → Task 3 Step 1/3 + Task 7 log). D12 (NFU git-URL pin → Task 2 Step 6). D13 (conformance test → Task 5). Docs/plan §5.7 → Task 6. Smoke log §5.6 → Task 7. AC mapping §10 → Task 8 Step 4 (human) + Tasks 2/5 (automated). **All spec decisions have a task.**
- **Placeholder scan:** the only intentional placeholders are `REPLACE_FROM_LOCKFILE` in Task 2 Step 5 (explicitly resolved from the lock file in the same task, with a "no placeholders left" gate) and the OQ1/OQ2 version pins (resolved by the human in Task 8). No "TBD"/"add error handling"/"similar to" placeholders.
- **Type consistency:** `ChainTypeEvm`/`KeyFormatHex` constants identical in Task 4 and Task 5. `PeakCrypto.KeyPair` (not `Models.KeyPair`) used for export in both. `InitOtpLoginResponse?` nullable handled in both. Method names match the verified §1.2 surface (`CompleteImportPrivateKeyAsync(bundle, chainType)`, `ExportPrivateKeyAsync(address, targetPublicKey)`, `PeakCrypto.EncryptPrivateKeyToBundle(params)`, `PeakCrypto.DecryptExportBundle(params)`). `.meta` GUIDs are consistent: script `…a1` referenced by the scene's `m_Script` and the EditorBuildSettings scene guid `…a3` matches `SampleScene.unity.meta`.
```
