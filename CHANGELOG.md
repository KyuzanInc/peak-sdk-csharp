# Changelog

All notable changes to `KyuzanInc.Peak.Sdk` are documented here. This project
adheres to [Semantic Versioning](https://semver.org/) (pre-1.0 alpha).

## [0.1.0-alpha.3]

### Fixed

- **HTTP requests now always send a `User-Agent` header.**
  `DefaultPeakHttpClient` (System.Net.Http) previously sent no `User-Agent`,
  which Peak's edge (nginx) rejects with `403 Forbidden`. The client now sets a
  default `User-Agent` of `KyuzanInc.Peak.Sdk/<version>` per request, only when
  the caller has not already supplied one. The value is derived from the
  assembly's informational version. The header is set on the outgoing
  `HttpRequestMessage` (never on the shared/injected `HttpClient`'s
  `DefaultRequestHeaders`), so it works with an injected `HttpClient` and never
  mutates caller-owned shared state.

## [0.1.0-alpha.2]

- Public `PeakCrypto`; closed the Turnkey type over the public surface.

## [0.1.0-alpha.1]

- STJ-only consumer response path; Unity/IL2CPP-consumable.

## [0.1.0-alpha.0]

- Initial pre-release.
