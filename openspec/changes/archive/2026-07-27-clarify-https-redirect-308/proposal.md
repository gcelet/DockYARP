## Why
nginx-proxy exposes `NON_GET_REDIRECT` because its default HTTP→HTTPS redirect is `301`, which turns a
non-GET request (e.g. POST) into a GET on replay — so operators can opt into `307`/`308`. DockYarp already
redirects with **308** (permanent, method-preserving) for **all** methods, so the non-GET problem does not
exist and a separate knob is unnecessary. This change locks that behavior in with a test and states it
explicitly in the spec and docs (a deliberate, better-than-default divergence), then closes the backlog item
as already covered.

## What Changes
- Add a unit test asserting a non-GET request is redirected with status **308** (method preserved).
- Clarify the `security` HTTPS-enforcement requirement to state the redirect uses 308 (method-preserving).
- Document in `docs/security-middleware.md` that DockYarp uses 308 for all redirects, so no `NON_GET_REDIRECT`
  knob is provided.
- No behavior change.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `security`: clarify that HTTP→HTTPS redirects use a 308 (permanent, method-preserving) status.

## Impact
- **Code**: no production change. Test in `tests/DockYarp.Security.Tests`; doc in `docs/security-middleware.md`.
- **Deferred**: a configurable redirect status (`301`/`302`/`307`/`308`) — low value since 308 works
  universally; not planned.
- **Owning agent**: AG-SEC.
