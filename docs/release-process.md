# Release process

This runbook governs source releases for `KyuzanInc.Peak.Sdk`. The source
repository is public, while the package is private GitHub Packages distribution
for explicitly authorized consumers. Source availability does not grant package
access. The dependency on `KyuzanInc.Turnkey.Sdk` is exactly `[1.0.0]`.

## Release procedure

1. Merge the approved release commit into `main` and ensure its required CI and
   review checks have passed.
2. Create a strict Semantic Versioning tag such as `v1.0.0` that targets the
   current approved `main` tip. The release workflow rejects tags behind or
   ahead of `main`. Do not retag or move an existing release tag.
3. Use the protected `github-packages` environment for publishing and the
   protected `github-packages-read` environment for credentialed validation.
   Read and publish credentials must be separate, least-privilege credentials.
4. Publish the immutable package version to the private GitHub Packages feed,
   then run the authorized consumer restore and smoke checks with the read
   credential. Never make the package public as part of this procedure.
5. Create the public GitHub Release with release notes and
   `release-checksums.txt` only. Public Releases contain checksum-only assets:
   never attach `.nupkg` or `.snupkg` files.

Package versions are immutable. If a released package needs correction, publish
a new patch version (for example, `1.0.1`); do not replace or delete the
existing version. This project does not publish packages to nuget.org.

## Administrator checklist

Before a release, an administrator confirms all of the following:

- `main` has branch/ruleset protection, required CI, and required review; force
  pushes and branch deletion are prevented.
- Secret scanning and push protection are enabled, and Dependabot plus CodeQL
  for C# are configured and reviewed.
- GitHub private vulnerability reporting is enabled.
- `KyuzanInc.Peak.Sdk` package visibility is private, package access
  inheritance is disabled, and the public source repository has no package
  Actions access.
- `github-packages` and `github-packages-read` are protected environments with
  separate, least-privilege credentials for publish and read operations.
- No package binaries are attached to public Releases or retained in Actions
  artifacts; public release assets are checksum-only.
- The source repository remains public and the package remains private, with
  authorized consumers explicitly granted package access.

If any check is not true, stop the release and correct the administration
setting before continuing.
