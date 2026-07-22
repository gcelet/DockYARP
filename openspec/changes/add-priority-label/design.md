## Context

`RouteRule.Priority` exists and the Core `RouteMatcher` already sorts by it, but discovery never populates
it and the YARP mapper never carries it — so priority is inert (every discovered route is `0`, and even a
non-zero priority would only affect the security matcher, not actual proxying). This wires the label through
and into YARP's `Order`.

## Goals / Non-Goals

**Goals:** parse `DOCKYARP_PRIORITY` into `RouteRule.Priority` (default `0`, non-numeric → `0` + warning),
and map priority to `RouteConfig.Order` so it governs request routing.

**Non-Goals:** per-path priority beyond the existing host+path model; changing the matcher's precedence rules
(exact > wildcard, priority, longest path) which already honor priority.

## Decisions

- **`DOCKYARP_PRIORITY` → `ContainerLabelConfig.Priority`** parsed by `LabelParser` (invariant int,
  default `0`). Mirroring the proto/auth pattern, a pure `LabelParser.HasInvalidPriority(labels)` lets the
  mapper warn on a non-numeric value while the parser stays side-effect free.
- **`ContainerMapper`** sets `RouteRule.Priority = first.Priority` and warns when the label is invalid.
- **`YarpConfigMapper`** maps priority to order as `Order = priority == 0 ? null : -priority`. Verified via
  Microsoft Learn: YARP routes with a **lower** order take precedence, so negating priority makes a higher
  priority win; `0` leaves the order unset (YARP default), unchanged from today.

## Risks / Trade-offs

- Negative orders are valid in YARP and simply sort before `0`; they do not collide with the default-host
  catch-all (`int.MaxValue - 1`) or the fallback (`int.MaxValue`), which stay lowest.

## Migration Plan

Additive: one label, one optional config field (default `0`), mapper wiring on both sides. Routes without
the label are unchanged (`Order` stays unset).

## Open Questions

- Exposing priority in the admin API route view — deferred to a later observability pass.
