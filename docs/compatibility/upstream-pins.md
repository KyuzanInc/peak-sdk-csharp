# Upstream pins

These values identify the reviewed upstream inputs. Complete upstream source
trees are not retained in this repository.

## Turnkey source

| Field | Value |
|---|---|
| Version | `1.0.0` |
| Commit | `5c219364d678c3a72786a2a89fbc7bbd4931be5e` |
| Archive SHA-256 | `9ea5b43e72b0c519f886b03375b59c3f36dbf2d6d2746b258a3adc1b5af672bb` |

The SDK consumes the private `KyuzanInc.Turnkey.Sdk` package at the exact
`[1.0.0]` dependency boundary.

## Historical Unity source

| Field | Value |
|---|---|
| Commit | `fc560e8b18bcc28b6a863520e82115513d6c8ced` |
| Source tree | Intentionally not retained |

`peak-sdk-unity` is maintained as a separate downstream adapter. The commit is
retained solely as historical provenance for the original compatibility work.

## OpenAPI

| Field | Value |
|---|---|
| Commit | `72ca08b3eade7117334ad481f6a1b29a6bee73a9` |
| Imported contract SHA-256 | `fe6f4d6a7c8d9fc93d0b36a754059b0722031a5fe33af3164fdcca0ff93c0596` |
| Path | `upstream-snapshots/peak-server-openapi/public-api.yaml` |
| Public contract SHA-256 | `ee167eab8283bfde66e53a241697c4644159f712498ec8e18bdaf829178c2fe4` |

The public checksum is also recorded by the single-line manifest at
`tests/UpstreamSources/peak-server-openapi-public-api.sha256`. Any mismatch is a
hard failure.
