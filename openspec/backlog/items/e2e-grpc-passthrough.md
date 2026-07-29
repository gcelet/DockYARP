---
id: e2e-grpc-passthrough
capability: yarp-dynamic-config
agent: AG-RP
tier: B-runtime
priority: low
status: backlog
nginx-proxy: (internal finding — runtime validation of VIRTUAL_PROTO=grpc)
provenance: deferred from add-grpc-passthrough, 2026-07-29
---

## Why
`add-grpc-passthrough` wired `VIRTUAL_PROTO=grpc`/`grpcs` to an HTTP/2-exact cluster and unit-tested the
config mapping, but did not prove an end-to-end gRPC round-trip. A gRPC backend fixture in the Aspire e2e suite
would validate that unary and streaming calls actually proxy (HTTP/2 + trailers) through DockYarp.

## nginx-proxy behavior
N/A — internal runtime-coverage finding. No `parity.md` row (the grpc row is already ✅ from the config change).

## DockYarp today
- `VIRTUAL_PROTO=grpc`/`grpcs` sets `Cluster.Http2Only`, mapped to YARP `HttpRequest.Version=2.0` /
  `VersionPolicy=RequestVersionExact` (`add-grpc-passthrough`). Covered by unit tests only.

## Proposed change (sketch)
- Add a small gRPC backend (a .NET gRPC service, or a ready-made gRPC echo image) to the Aspire e2e AppHost,
  labeled `VIRTUAL_PROTO=grpc`.
- From the e2e harness, make a gRPC call through DockYarp's HTTPS endpoint (HTTP/2) and assert a unary response,
  and ideally a streaming response (trailers).
- Integrate into an existing TLS scenario where possible (a gRPC host over the step-ca cert), per the testing
  pyramid — do not stand up a whole new suite.

## Acceptance criteria (→ scenarios)
- **WHEN** a gRPC backend is labeled `VIRTUAL_PROTO=grpc` and a client makes a unary gRPC call through DockYarp
- **THEN** the call succeeds end to end (HTTP/2, trailers preserved)
- **WHEN** a server-streaming gRPC call is made **THEN** the stream is proxied to completion

## Notes / risks / references
- Internal finding — no `parity.md` row.
- Needs a gRPC backend fixture; verify the Aspire container/HTTP-2 wiring via the aspire MCP first.
- Sibling (done): `add-grpc-passthrough` (config mapping).
