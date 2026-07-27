---
id: add-grpc-passthrough
capability: yarp-dynamic-config
agent: AG-RP
tier: B-runtime
priority: low
status: backlog
nginx-proxy: VIRTUAL_PROTO=grpc
provenance: this parity pass (matrix: proto grpc ⛔)
---

## Why
nginx-proxy supports `VIRTUAL_PROTO` values beyond http/https, including `grpc`. DockYarp maps only http/https;
gRPC backends cannot be declared, even though YARP proxies gRPC well (it is HTTP/2 with trailers).

## nginx-proxy behavior
- `VIRTUAL_PROTO=grpc` (also `grpcs`) makes nginx use `grpc_pass`, forwarding gRPC over HTTP/2 to the backend.

## DockYarp today
`VIRTUAL_PROTO` accepts `https`→Https else Http; other values fall back to http + warn
(`src/DockYarp.Docker/Labels/LabelParser.cs:202-205,102-109`). `BackendScheme` has no gRPC notion.

## Proposed change (sketch)
gRPC over YARP mostly needs the backend cluster to negotiate HTTP/2 end-to-end and trailers to flow (YARP does
this by default when the request is HTTP/2). Add a `grpc`/`grpcs` `VIRTUAL_PROTO` value that (a) selects the
http/https scheme and (b) forces the cluster's `HttpRequest.Version = 2.0` / `VersionPolicy = ExactVersion`.
Validate with a gRPC backend in the e2e suite.

## Acceptance criteria (→ scenarios)
- **WHEN** `VIRTUAL_PROTO=grpc` **THEN** the cluster forwards requests over HTTP/2 and gRPC unary + streaming
  calls succeed end to end.
- **WHEN** `VIRTUAL_PROTO=grpcs` **THEN** the backend is reached over TLS/HTTP2.

## Notes / risks / references
- Confirm YARP HTTP/2 + trailers config; needs a gRPC backend fixture (Aspire e2e). fastcgi/uwsgi remain
  non-goals (not HTTP).
