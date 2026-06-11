# peak-sdk-unity-reference

A minimal Unity 6000.0.x project that consumes **KyuzanInc.Peak.Sdk** from GitHub
Packages and demonstrates the full OTP login + wallet import/export flow through
an IMGUI UI. Its purpose is an **IL2CPP AOT smoke**: prove that
`HttpClient` + System.Text.Json source-gen + `async/await` + BouncyCastle survive
IL2CPP stripping in a real Player build.

> ⚠️ Use a **test** Peak environment, a **test** `projectApiKey`, and **test**
> private keys only. The API key is entered at runtime and is never stored in the
> scene. Never paste a production key or a real wallet's private key.

## 1. Configure GitHub Packages auth (one-time)

During internal testing, `KyuzanInc.Peak.Sdk` and its transitive
`KyuzanInc.Turnkey.Sdk` are pulled from **GitHub Packages** (the `github-kyuzan`
source declared in `Assets/NuGet.config`). Configure a `read:packages` credential
for that source **once** in your machine-global `~/.nuget` config, per
[`docs/development.md`](../../docs/development.md) — NuGetForUnity injects that
global credential at restore time. Everything else comes from nuget.org.

> When `KyuzanInc.Peak.Sdk` is published to nuget.org, this step (and the
> `github-kyuzan` source in `Assets/NuGet.config`) can be dropped — restore will
> work straight from nuget.org with no auth.

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
- `Assets/NuGet.config` — package sources (nuget.org + `github-kyuzan`). **NuGetForUnity reads this file, not a project-root `NuGet.config`,** and injects the GitHub Packages credential from your global `~/.nuget` config.
- `Assets/link.xml` — IL2CPP preservation for BouncyCastle + Turnkey.
