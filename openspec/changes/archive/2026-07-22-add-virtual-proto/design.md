## Context

nginx-proxy's `VIRTUAL_PROTO` picks the backend transport scheme. DockYarp hard-codes `http://` when
building a cluster endpoint address (`ContainerMapper`), so it cannot reach HTTPS backends. The endpoint
`Address` is already absolute and scheme-bearing and is consumed as-is by the YARP mapper, so the scheme is
best expressed *through the address* rather than as a second, drift-prone field.

## Goals / Non-Goals

**Goals:** model an endpoint's backend scheme (`http`/`https`), parse `VIRTUAL_PROTO` into it (default `http`,
unsupported values fall back to `http` with a warning), and build the endpoint address with the chosen
scheme so YARP proxies to HTTPS backends unchanged.

**Non-Goals:** `grpc`/`grpcs`/`fastcgi`/`uwsgi` (deferred), per-backend TLS validation / client certs,
`VIRTUAL_HOST_MULTIPORTS`.

## Decisions

- **`BackendScheme` enum in Core** (`Http`, `Https`). A first-class model concept rather than a bare string.
- **`ClusterEndpoint.Create(id, scheme, host, port)` factory** composes the absolute address from the scheme.
  The record shape stays `(Id, Address)` so `Address` remains the single source of truth (no duplicated
  `Scheme` field to keep in sync) and every existing construction/`.Address` read is untouched. This is how
  the model "carries a scheme": the scheme flows into the address through a model-owned factory.
- **`ContainerLabelConfig.Scheme`** (default `Http`) parsed by `LabelParser` from `VIRTUAL_PROTO`
  (case-insensitive); anything other than `http`/`https` yields `Http`. Mirroring the auth pattern, a pure
  `LabelParser.HasUnsupportedProto(labels)` lets the mapper emit the warning (parser stays side-effect free).
- **`ContainerMapper`** builds endpoints via `ClusterEndpoint.Create(..., config.Scheme, ...)` and warns when
  `HasUnsupportedProto` is true.

## Risks / Trade-offs

- No duplicate scheme field means callers can't read the scheme back without parsing the address; nothing
  needs that today (YARP takes the absolute address), so we avoid the drift risk of two sources of truth.
- HTTPS backend certificate validation uses YARP/Kestrel defaults; hardening (skip-verify, client certs) is
  a separate backlog item and out of scope here.

## Migration Plan

Additive: new enum, a new factory (existing constructor kept), one optional config field, mapper wiring.
No config or persisted state changes.

## Open Questions

- `grpc`/`fastcgi`/`uwsgi` protocols and backend TLS hardening — deferred to their own backlog changes.
