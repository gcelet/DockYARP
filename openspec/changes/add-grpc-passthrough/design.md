# Design — add-grpc-passthrough

## Mapping `VIRTUAL_PROTO`
| `VIRTUAL_PROTO` | Backend address scheme | Cluster HTTP/2 |
|---|---|---|
| `http` (default) | `http://` | no |
| `https` | `https://` | no |
| `grpc` | `http://` | yes (h2c) |
| `grpcs` | `https://` | yes (h2 over TLS) |

`LabelParser` classifies the scheme (`grpc`→Http, `grpcs`→Https) and a separate `Http2` flag (`grpc`/`grpcs`).
`grpc`/`grpcs` are added to the recognized proto set so discovery no longer warns on them.

## Cluster + YARP
`Cluster` gains `Http2Only`. `YarpConfigMapper.BuildCluster` sets `HttpRequest.Version = 2.0` and
`VersionPolicy = RequestVersionExact` when `Http2Only` is set (combined with any existing `ActivityTimeout`).
RequestVersionExact avoids YARP's default downgrade so h2c/prior-knowledge HTTP/2 is used; YARP forwards gRPC
trailers by default, so no extra transform is needed.

## Why this is enough for gRPC
- Client → proxy: gRPC clients connect over TLS, and the HTTPS endpoint already negotiates HTTP/2. (Plaintext
  h2c **inbound** is not supported — the plaintext endpoint is HTTP/1.1 only — but that is not how gRPC clients
  connect.)
- Proxy → backend: the cluster now speaks HTTP/2 exactly, which is what a gRPC upstream requires.

## Scope
- Classic single-host `VIRTUAL_PROTO=grpc`/`grpcs`. Multiports gRPC (per-entry proto) is not wired here.
- The runtime gRPC round-trip (unary + streaming) needs a gRPC backend in the Aspire e2e suite; that is deferred
  to a new backlog item `e2e-grpc-passthrough`. This change is unit-tested at the config-mapping level.

## Testing
- `LabelParser`: `grpc` → Http + Http2; `grpcs` → Https + Http2; `https` → Https + not Http2; `grpc`/`grpcs`
  are not reported as unsupported protos.
- `YarpConfigMapper`: an `Http2Only` cluster maps to `HttpRequest.Version == 2.0` and
  `VersionPolicy == RequestVersionExact`.
