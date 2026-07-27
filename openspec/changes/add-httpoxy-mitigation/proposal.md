## Why
The httpoxy vulnerability lets a client set a `Proxy` request header, which some CGI/backend runtimes read as
`HTTP_PROXY` and then use to route their outbound traffic through an attacker. nginx-proxy strips the inbound
`Proxy` header. DockYarp forwards request headers through YARP and does **not** remove `Proxy` (it is not in
YARP's excluded set), so a malicious value can reach a backend.

## What Changes
- Strip the inbound `Proxy` request header before proxying, in the forwarded-headers request transform.
- Integration test: a client-supplied `Proxy` header does not reach the backend.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `yarp-dynamic-config`: strip the inbound `Proxy` request header (httpoxy mitigation).

## Impact
- **Code**: `src/DockYarp.App/ReverseProxy/ForwardedHeadersTransform.cs`. Test in
  `tests/DockYarp.IntegrationTests`.
- **Deferred**: none.
- **Owning agent**: AG-RP.
