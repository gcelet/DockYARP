# Design — add-loadbalance-policies

## Context
`LoadBalancingPolicy` (Core) has `RoundRobin` and `LeastRequests`. `LabelParser.ParsePolicy` normalizes
`DOCKYARP_LB` (strips `-`, upper-cases) and maps those two, else `null` (→ round-robin default).
`YarpConfigMapper.MapPolicy` maps to hardcoded strings. YARP 2.3 exposes five built-in policies via
`LoadBalancingPolicies`: `RoundRobin`, `LeastRequests`, `PowerOfTwoChoices`, `Random`, `FirstAlphabetical`.

## Decisions

### 1. Expose the three remaining built-in policies
Add `PowerOfTwoChoices`, `Random`, `FirstAlphabetical` to the enum, to `ParsePolicy`
(`POWEROFTWOCHOICES`/`RANDOM`/`FIRSTALPHABETICAL` after normalization), and to `MapPolicy`. `MapPolicy`
references YARP's own `LoadBalancingPolicies` constants rather than local string literals, so the mapped value
can never drift from YARP's accepted names.

### 2. Warn on an unrecognized value
Add `LabelParser.HasUnsupportedLoadBalancing` (mirrors `HasUnsupportedHttpsMethod`) and a `ContainerMapper`
warning `unrecognized DOCKYARP_LB; using round-robin`. The fallback itself is unchanged (round-robin).

### 3. Client-affinity / `ip_hash` is out of scope → `add-session-affinity`
nginx `ip_hash` is sticky-by-client, which YARP does **not** offer as a load-balancing policy. The nearest
YARP feature is **session affinity** (cookie / custom-header), a separate mechanism that protects its affinity
cookie with **Data Protection**. Enabling it makes YARP the **first real DP consumer**, which per the backlog
requires the DP encryption certificate and a startup fail-fast when it is absent. That cross-cutting work does
not belong in a pure policy-mapping change, so it is split to a new backlog item `add-session-affinity`
(carrying the Data Protection gate). This change stays a clean, unit-testable mapping — consistent with using
YARP's own extension points rather than reinventing load balancing.

## Verification
- Unit only: `ParsePolicy`/`HasUnsupportedLoadBalancing` (Docker.Tests), `MapPolicy` (IntegrationTests),
  `ContainerMapper` warning. No runtime/e2e needed — the mapping is deterministic and YARP applies the policy.

## Risks
- Policy **behavior** (e.g. `PowerOfTwoChoices` vs `RoundRobin` distribution) is YARP's; we only select it, so
  there is nothing new to validate at runtime here.
