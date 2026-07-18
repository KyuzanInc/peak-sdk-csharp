# Development

## Prerequisites

- .NET SDK 8.0.x (`dotnet --version`)
- `curl` and `tar` for the checksum-pinned public Turnkey source archive

`KyuzanInc.Turnkey.Sdk [1.0.0]` is a private GitHub Packages dependency.
Source visibility does not grant package access. Fine-grained tokens are not
used for this setup. Package credentials are required for consuming or
releasing the published package, but not for the standard source-compatibility
checks below.

## GitHub Packages authentication

The repository does not store credentials. Configure them in your user-level
NuGet configuration, not in a committed file:

```bash
export GITHUB_TOKEN=ghp_... # classic PAT with read:packages and package access

dotnet nuget add source https://nuget.pkg.github.com/KyuzanInc/index.json \
  --name github-kyuzan \
  --username <your-github-username> \
  --password "$GITHUB_TOKEN" \
  --store-password-in-clear-text \
  --configfile ~/.nuget/NuGet/NuGet.Config
```

On later token refreshes, use `dotnet nuget update source github-kyuzan` with
the same username, password, and config-file arguments. If restore returns
`401 Unauthorized`, confirm `read:packages`, explicit package access, and the
authorization of any private-repository token.

## Build and test

```bash
./tools/compatibility/prepare-turnkey-local-feed.sh \
  .artifacts/turnkey-feed .artifacts/turnkey-source
export TurnkeySourceProject="$PWD/.artifacts/turnkey-source/src/turnkey-sdk-csharp.csproj"
dotnet restore peak-sdk-csharp.sln --force-evaluate \
  --configfile nuget.public-ci.config
dotnet build peak-sdk-csharp.sln -c Release --no-restore
dotnet test peak-sdk-csharp.sln -c Release --no-build --filter "Category!=E2E"
dotnet format peak-sdk-csharp.sln --verify-no-changes --no-restore \
  --exclude packages/peak-public-api-client-csharp/ .artifacts/turnkey-source/
unset TurnkeySourceProject
```

The contributor path compiles against the checksum-pinned public Turnkey 1.0.0
source and writes separate ignored lock files under `obj/`; it never exposes a
private package credential to pull-request code. The committed
`packages.lock.json` files remain the published-package source of truth:
`verify-version-contract.sh` requires the exact version, range, and published
package SHA-512. The trusted release workflow performs the locked restore
against the published package. CI enables `ContinuousIntegrationBuild`.

## E2E tests

E2E tests need dedicated credentials and are not part of the standard
contributor command:

```bash
export TURNKEY_TEST_ORG_API_KEY=...
export TURNKEY_TEST_ORG_ID=...
dotnet test peak-sdk-csharp.sln --filter "Category=E2E"
```

Without those variables, the E2E suite is skipped.

## Consuming the package

`KyuzanInc.Peak.Sdk 1.0.0` is distributed from private GitHub Packages for
explicitly authorized consumers. With the `github-kyuzan` source configured,
install the stable version:

```bash
dotnet add package KyuzanInc.Peak.Sdk --version 1.0.0
```

The project also requires the same authorized source for the exact transitive
dependency `KyuzanInc.Turnkey.Sdk [1.0.0]`. This project does not publish to
nuget.org.

## Local package feed

For offline development or an unreleased local build, create a local feed:

```bash
dotnet pack peak-sdk-csharp.sln -c Release -o ./local-feed
```

Point a downstream `nuget.config` at the absolute `local-feed` path and add the
desired local `KyuzanInc.Peak.Sdk` version. The downstream project still needs
authorized GitHub Packages access for the transitive Turnkey dependency.

## Updating Turnkey

See [docs/sync-rules.md](sync-rules.md). A dependency update changes
`Directory.Packages.props`, refreshes lock files, runs the wire-format smoke
test, and links the upstream release notes in the pull request.

## Cross-platform storage

- Windows: DPAPI is the default `ISecureStorage` implementation.
- macOS and Linux: no built-in secure storage implementation is provided.
