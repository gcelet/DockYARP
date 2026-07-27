## Context
Proxied requests are shaped by `ForwardedHeadersTransform.Apply`
(`src/DockYarp.App/ReverseProxy/ForwardedHeadersTransform.cs`): it sets `X-Forwarded-*`, `X-Real-IP`, and
`X-Forwarded-Port`. YARP copies request headers to the backend by default, and the `Proxy` header is not in
YARP's excluded set — so it is forwarded today.

## Goals / Non-Goals
- **Goal**: guarantee a client-supplied `Proxy` request header never reaches a backend.
- **Non-Goal**: a broader request-header allow-list.

## Decisions
- Remove the `Proxy` header explicitly in the existing request transform — defense-in-depth, independent of
  YARP's default excluded set (so the guarantee holds regardless of YARP version).

## Risks / Trade-offs
- None: `Proxy` is not a legitimate end-to-end request header.

## Migration Plan
- None.

## Open Questions
- None.
