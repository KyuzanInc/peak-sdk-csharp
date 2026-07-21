# ADR-0001: Source provenance and compatibility

- Status: Accepted
- Date: 2026-07-18

## Context

The public repository must preserve reproducible provenance without publishing
complete copies of internal or upstream repositories. A reviewer must be able
to verify the retained input and distinguish an intentional update from drift.

## Decision

- Provenance is exact commit plus checksum, not a complete copied repository.
- Upstream inputs are reviewed and changed only through a dedicated PR.
- A checksum mismatch is a hard failure.

## Consequences

The historical Unity source tree is not retained. The OpenAPI input has both an
imported checksum and a checksum for the sanitized public contract. Updating an
input requires explicit provenance records, regeneration, and publication-gate
verification.
