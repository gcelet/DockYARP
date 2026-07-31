## Why
DockYarp exposes only round-robin and least-requests, while YARP ships three more built-in load-balancing
policies (`PowerOfTwoChoices`, `Random`, `FirstAlphabetical`). Operators cannot select them per container via
`DOCKYARP_LB`.

## What Changes
- Expose YARP's remaining built-in policies through `DOCKYARP_LB`: `power-of-two-choices`, `random`,
  `first-alphabetical` (in addition to `round-robin` and `least-requests`).
- An unrecognized `DOCKYARP_LB` value falls back to round-robin **with a warning** (the fallback behavior is
  unchanged; the warning is added for parity with the other unsupported-label diagnostics).
- The model→YARP cluster mapping uses YARP's own `LoadBalancingPolicies` constants (no drift).

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `yarp-dynamic-config`: per-cluster load balancing supports all of YARP's built-in policies.

## Impact
- **Code**: `DockYarp.Core` (`LoadBalancingPolicy` enum + 3 values); `DockYarp.Docker` (`LabelParser.ParsePolicy`
  + `HasUnsupportedLoadBalancing`, `ContainerMapper` warning); `DockYarp.App` (`YarpConfigMapper.MapPolicy` via
  `LoadBalancingPolicies` constants).
- **Tests**: `LabelParser` (each new value parses; unknown → null + flagged unsupported), `YarpConfigMapper`
  (each policy maps to the matching YARP policy string), `ContainerMapper` (unknown `DOCKYARP_LB` warns).
- **Split out**: **client-affinity / nginx `ip_hash`** is *not* a YARP load-balancing policy — the closest
  equivalent is YARP **session affinity**, which protects its cookie with **Data Protection** and would become
  the first real DP consumer (requiring the DP certificate + fail-fast at startup). That is a distinct,
  cross-cutting change, moved to a new backlog item `add-session-affinity`.
- **Owning agent**: AG-RP. Resolves `add-loadbalance-policies` (LB policies); affinity → `add-session-affinity`.
