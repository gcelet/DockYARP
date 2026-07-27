## Context
`RouteMatcher` (`src/DockYarp.Core/Routing/RouteMatcher.cs`) keys exact hosts in a case-insensitive
dictionary and looks them up by the request host. `VIRTUAL_HOST` is parsed verbatim (no DNS validation), so an
IPv4 literal is just another exact host key.

## Goals / Non-Goals
- **Goal**: make IPv4 raw-IP `VIRTUAL_HOST` support explicit and test-locked; close the backlog item.
- **Non-Goal**: IPv6-literal hosts. `HttpContext.Request.Host.Host` returns the IPv6 address in brackets
  (`[::1]`), so matching an IPv6 literal needs normalization — tracked separately if wanted.

## Decisions
- Keep the existing behavior; add a `RouteMatcher` regression test for an IPv4-literal host.
- Document the IPv4 support (and the IPv6 caveat) in the labels reference.

## Risks / Trade-offs
- None (no behavior change).

## Migration Plan
- None.

## Open Questions
- Whether IPv6-literal hosts are worth a follow-up (bracket normalization).
