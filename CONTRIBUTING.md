# Contributing

Thank you for improving the public source for `KyuzanInc.Peak.Sdk`. Please use
an issue or discussion to align on a substantial change before investing in an
implementation.

## Local verification

Prepare the authorized local Turnkey feed, then run the same non-E2E checks
used for a contribution:

```bash
./tools/compatibility/prepare-turnkey-local-feed.sh
dotnet restore peak-sdk-csharp.sln --locked-mode --configfile nuget.public-ci.config
dotnet build peak-sdk-csharp.sln -c Release --no-restore
dotnet test peak-sdk-csharp.sln -c Release --no-build --filter "Category!=E2E"
dotnet format peak-sdk-csharp.sln --verify-no-changes --no-restore \
  --exclude packages/peak-public-api-client-csharp/
```

## Pull requests

- Use a conventional PR title, for example `fix: reject an invalid response`.
- Do not commit secrets, credentials, tokens, private keys, session JWTs, or
  customer data.
- Generated changes must identify their public source and regeneration command
  in the PR description; do not use unpublished or internal provenance.
- Do not commit package binaries (`.nupkg` or `.snupkg`). Packages are produced
  by the release process and are distributed only through the authorized private
  GitHub Packages feed.
- Keep changes focused, include tests when behavior changes, and make sure the
  required CI and review checks are green before requesting review.
