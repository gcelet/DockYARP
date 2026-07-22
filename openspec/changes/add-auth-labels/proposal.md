## Why

Basic Auth is implemented (model + middleware) but unreachable: Docker discovery never populates
`RouteRule.Auth`, so no container can request protection. This adds the auth labels, matching nginx-proxy's
htpasswd-per-vhost capability.

## What Changes

- Parse `DOCKYARP_AUTH_USER`, `DOCKYARP_AUTH_PASSWORD`, and optional `DOCKYARP_AUTH_REALM` from
  container labels into `RouteRule.Auth` (`BasicAuthCredentials`).
- Missing/partial auth labels (e.g. user without password) are treated as invalid: logged and the route is
  left unprotected (no crash), consistent with the existing validation behavior.

## Capabilities

### Modified Capabilities
- `docker-discovery`: container labels can configure Basic Auth credentials for the route.

## Impact

- **Code**: `src/DockYarp.Docker` (`DockerLabels`, `LabelParser`, mapping to `RouteRule.Auth`).
- **Enables**: the existing `security` Basic Auth middleware end-to-end.
- **Owning agent**: AG-DD / AG-SEC.
