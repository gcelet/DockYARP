## Context

`RouteMatcher.TryMatch` looks up the exact-host bucket first and only falls through to wildcard routes when no
exact host matches, so **exact beats wildcard regardless of priority**. Within a single host's bucket, routes
are sorted by priority (desc) then path length (desc). YARP's endpoint selection is consistent (exact host
and longer path outrank wildcard/shorter for the proxying). nginx-proxy relies on nginx `server_name`
(exact > wildcard) and `location` (longest prefix) and has no priority label.

A live e2e run confirmed this: with `*.priority.local` (priority 10) and `exact.priority.local` (priority 1),
a request to `exact.priority.local` was answered by the **exact** backend — priority did not override host
specificity.

## Goals / Non-Goals

**Goals:** make the e2e suite sound; state the real (nginx-matching) precedence in the spec; document
`DOCKYARP_PRIORITY` as a DockYarp extension.

**Non-Goals:** changing route-selection behaviour (it already matches nginx-proxy for host/path); making
priority override host specificity (that would diverge from nginx-proxy); the Nuke `Test` flake (separate).

## Decisions

- **Drop the e2e priority scenario.** Priority is not observable through label-based route competition: the
  only way to create two matching routes (wildcard vs exact) differs by specificity, which correctly wins, and
  same-host containers are aggregated into one cluster. Priority is unit-tested where it is deterministic.
- **Clarify, don't change, the spec.** Reword "Host and path matching" so precedence reads host-specificity-
  first, then priority/path within a host, and add a scenario "Exact host wins over a higher-priority
  wildcard" documenting the verified behaviour.
- **Document the extension.** `DOCKYARP_PRIORITY` has no nginx-proxy equivalent; note it in the labels
  reference and the parity matrix, and record that host/path selection matches nginx-proxy.

## Risks / Trade-offs

- Removing the e2e scenario slightly reduces e2e breadth, but it was testing behaviour that neither the
  product nor nginx-proxy exhibits; the honest coverage is the in-process priority tests.

## Migration Plan

Docs and tests only; the `proxy-routing` requirement text is clarified to match existing behaviour. No code or
behaviour change.

## Open Questions

- None. (Within-host priority-over-path is a DockYarp extension beyond nginx-proxy; kept as-is and
  documented, not changed.)
