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
Turnkey nupkg, add the `github-kyuzan` source to `Assets/NuGet.config` (the file
NuGetForUnity actually reads — a project-root `NuGet.config` is ignored) and let
NuGetForUnity pull Turnkey from GitHub Packages (mirrors the repo's
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

The committed `ProjectSettings` already pin **Scripting Backend = IL2CPP**, **Api
Compatibility Level = .NET Standard 2.1**, and **Managed Stripping Level = Low**
for Standalone (the project opens + compiles clean in Unity 6000.0.73f1 — verified
via the Editor CLI, see the smoke log). Build a Standalone Player and run the same
flow on it. Record the result (and any `link.xml` / stripping-level finding) in
`docs/operations/il2cpp-smoke-<date>.md`.

## Files

- `Assets/Scripts/PeakExampleDemo.cs` — the MonoBehaviour (IMGUI + `async void`).
- `Assets/packages.config` — the full NuGet dependency closure (NuGetForUnity is not transitive).
- `Assets/NuGet.config` — package sources incl. the `../LocalFeed` local feed. **NuGetForUnity reads this file, not a project-root `NuGet.config`.**
- `Assets/link.xml` — IL2CPP preservation for BouncyCastle + Turnkey.
- `prepare-feed.sh` — packs the SDK + vendors the Turnkey nupkg into `LocalFeed/`.
