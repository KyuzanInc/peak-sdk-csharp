# Changelog

All notable changes to `KyuzanInc.Peak.Sdk` are documented here. This project
adheres to [Semantic Versioning](https://semver.org/).

## [1.0.0]

- Established the stable public API for `KyuzanInc.Peak.Sdk`.
- Pinned `KyuzanInc.Turnkey.Sdk` to the exact `[1.0.0]` dependency version.
- Prepared public source provenance and documentation for external maintenance.
- Established the public-source, private-GitHub-Packages distribution boundary
  for explicitly authorized consumers.

## [0.1.0-alpha.4]

### Security

- `DefaultPeakHttpClient` debug logs now record only the HTTP method and
  endpoint. Serialized POST bodies, which can contain OTP or key material, are
  never logged.
- HTTP and JSON parse errors preserve status, method, and endpoint context
  without retaining the raw response body. The public `RawResponseBody`
  property remains for source compatibility and is `null` for errors raised by
  the default client.
- Release publishing is restricted to GitHub Packages. The dormant NuGet.org
  push path has been removed from the publish workflow.

### Fixed

- The test project now explicitly identifies itself to the current
  `Microsoft.NET.Test.Sdk`, preventing `dotnet test` from silently skipping the
  suite.

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
