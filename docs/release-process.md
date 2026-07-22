# Release process

This runbook governs source releases for `KyuzanInc.Peak.Sdk`. The source
repository is public, while the package is private GitHub Packages distribution
for explicitly authorized consumers. Source availability does not grant package
access. The dependency on `KyuzanInc.Turnkey.Sdk` is exactly `[1.0.0]`.

## Release procedure

1. Merge the approved release commit into `main` and ensure its required CI and
   review checks have passed.
2. Create the strict Semantic Versioning tag `v1.0.1` that targets the
   current approved `main` tip. The release workflow rejects tags behind or
   ahead of `main`. Do not retag or move an existing release tag.
3. Create the public GitHub Release record for the `v1.0.1` tag. Do not attach
   package binaries; the release workflow uploads only the checksum manifest.
4. The published Release event triggers the reviewed `release.yml` workflow.
   It checks out the immutable tag, performs the locked restore, build, tests,
   package validation, and byte-stable package canonicalization. Publish the
   immutable package version to the private GitHub Packages feed only from that
   workflow. The workflow uploads `release-checksums.txt` and no `.nupkg` or
   `.snupkg` Release assets.
5. After the Release workflow succeeds, run the authorized consumer restore and
   smoke checks through the protected `github-packages-read` environment with a
   dedicated read credential. The exact Peak and Turnkey versions must both be
   Peak `1.0.1` and Turnkey `1.0.0`.
6. Disable package repository-permission inheritance. Confirm that the package
   is private and that the source repository has no package Actions access.
7. Confirm the protected `github-packages` and `github-packages-read`
   environments still provide separate, least-privilege publish and read
   credentials. Public-repository releases fail closed when any dedicated
   credential is unavailable.
8. Reconfirm public visibility, secret scanning, push protection, Dependabot
   security updates, CodeQL default setup for C# and Actions, and private
   vulnerability reporting.

Package versions are immutable. If a released package needs correction, publish
a new patch version (for example, `1.0.1`); do not replace or delete the
existing version. This project does not publish packages to nuget.org.

## Administrator checklist

Before creating the Release, an administrator confirms that `main`
has branch/ruleset protection, required CI, and required review; force pushes
and branch deletion are prevented. Release tags are protected against update
and deletion, and the two package environments admit only the reviewed tag or
branch. The source repository must remain public and the
`KyuzanInc.Peak.Sdk` package must remain private.

After the Release and consumer workflows succeed, but before changing source
visibility, confirm all of the following:

- Package access inheritance is disabled, the package remains private, and the
  source repository has no package Actions access.
- `github-packages` and `github-packages-read` are protected environments with
  separate, least-privilege credentials for future publish and read operations.
- No package binaries are attached to the Release or retained in Actions
  artifacts; the only public release asset is `release-checksums.txt`.
- The isolated consumer restored and ran exact Peak `1.0.1` with exact Turnkey
  `1.0.0` from the authorized GitHub Packages source.

After the Release, verify that the source repository remains public, the package
remains private, and only explicitly authorized consumers retain package
access. Also verify secret scanning, push protection, Dependabot security
updates, CodeQL for C# and Actions, and private vulnerability reporting. If any
check is not true, stop and correct the administration setting before
continuing.
