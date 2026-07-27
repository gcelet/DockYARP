## Why
nginx-proxy accepts a bare IP address as `VIRTUAL_HOST`. DockYarp already supports this for IPv4: the route
matcher indexes exact hosts in a case-insensitive dictionary, and the label parser stores `VIRTUAL_HOST`
verbatim (no DNS validation), so an IPv4 `VIRTUAL_HOST` matches a request whose `Host` header is that IP. This
change locks the behavior with a test and documents it, then closes the backlog item. IPv6 literals (with
their bracketed `Host` form) are noted as a caveat and left as a follow-up.

## What Changes
- Add a `RouteMatcher` unit test: a route whose host is an IPv4 literal matches a request to that IP.
- Document that `VIRTUAL_HOST` may be a bare IPv4 address.
- No behavior change.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `proxy-routing`: clarify that an exact host match supports a bare IPv4 `VIRTUAL_HOST`.

## Impact
- **Code**: no production change. Test in `tests/DockYarp.Core.Tests`; doc in `docs/labels-reference.md`.
- **Deferred**: IPv6-literal hosts (bracketed `Host` handling) — follow-up if needed.
- **Owning agent**: AG-RP.
