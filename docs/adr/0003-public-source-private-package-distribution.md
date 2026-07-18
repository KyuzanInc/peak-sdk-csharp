# ADR-0003: Public source and private package distribution

- Status: Accepted
- Date: 2026-07-18

## Context

Source transparency and package access control are separate publication
concerns. Public workflows must not inherit access to private dependencies or
publish private package artifacts.

## Decision

- Source repository public; `KyuzanInc.Peak.Sdk` package private.
- `KyuzanInc.Turnkey.Sdk` 1.0.0 is an exact private-package dependency.
- Public Releases expose only `release-checksums.txt`.
- Public source workflows receive no inherited package access.

## Consequences

Restoring private packages requires explicit least-privilege credentials.
Public releases do not contain NuGet packages, and untrusted public workflows
cannot use inherited package credentials.
