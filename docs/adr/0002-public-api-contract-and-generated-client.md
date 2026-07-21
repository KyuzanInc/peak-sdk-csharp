# ADR-0002: Public API contract and generated client

- Status: Accepted
- Date: 2026-07-18

## Context

The OpenAPI description is useful for deterministic compatibility checks, but
generated models are not the SDK runtime contract and imported examples can
contain publication-sensitive values.

## Decision

- `public-api.yaml` is the only committed OpenAPI contract.
- Generated client types remain internal and build-time-only.
- Runtime responses continue to use hand-written System.Text.Json DTOs.
- Private hosts, identities, and internal-only operations are publication blockers.

## Consequences

Every import is sanitized before generation. OpenAPI Generator 7.9.0 regenerates
the internal client deterministically, while `PeakJsonContext` and the
hand-written DTOs remain the runtime serialization boundary.
