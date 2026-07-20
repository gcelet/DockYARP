## Why

Once TLS is available, HTTP traffic should be upgraded and sensitive routes protected, matching
nginx-proxy behavior. Security headers harden every proxied response.

> Status: **sketch** — proposal + spec intent only. Design and tasks to be detailed just-in-time when
> this phase starts.

## What Changes

- Add HTTP→HTTPS redirect middleware, configurable per host, active when a certificate is available.
- Add Basic Auth middleware configured via labels (user/password/realm) to protect routes.
- Add HSTS and common security headers, configurable, applied to proxied responses.

## Capabilities

### New Capabilities
- `security`: per-host HTTPS enforcement, label-driven Basic Auth, and HSTS/security headers.

### Modified Capabilities
<!-- None. -->

## Impact

- **Code**: `src/DockYarp.Security` middleware + pipeline wiring in `DockYarp.App`.
- **Upstream**: benefits from `add-tls-acme` (TLS availability) and reads auth labels from `docker-discovery`.
- **Owning agent**: AG-SEC.
