---
id: add-loadbalance-policies
capability: yarp-dynamic-config
agent: AG-RP
tier: A-structural
priority: low
status: backlog
nginx-proxy: com.github.nginx-proxy.nginx-proxy.loadbalance (label)
provenance: this parity pass (untracked feature; nginx test_loadbalancing)
---

## Why
nginx-proxy lets a container override the round-robin algorithm for its upstream via the
`...loadbalance` label (e.g. `hash $remote_addr;` for sticky-by-client). DockYarp exposes only round-robin and
least-requests, so client-affinity / hashing policies are unavailable.

## nginx-proxy behavior
- Label `com.github.nginx-proxy.nginx-proxy.loadbalance` injects an nginx upstream directive
  (`nginx.tmpl:749`), e.g. `ip_hash;` or `hash $remote_addr consistent;`.

## DockYarp today
`DOCKYARP_LB` maps to YARP round-robin / least-requests (`LabelParser.cs:274-282`); no hashing/affinity policy.

## Proposed change (sketch)
Map additional YARP `LoadBalancingPolicy` values where they exist (YARP ships `PowerOfTwoChoices`, `Random`,
`FirstAlphabetical`, `RoundRobin`, `LeastRequests`). For client-affinity, evaluate YARP **session affinity**
(cookie/custom-header) as the closest equivalent to nginx `ip_hash`. Extend the `DOCKYARP_LB` value set +
parser and the model→YARP cluster mapping.

## Acceptance criteria (→ scenarios)
- **WHEN** `DOCKYARP_LB=power-of-two-choices` **THEN** the cluster uses that YARP policy.
- **WHEN** an affinity mode is requested **THEN** repeated requests from one client stick to one endpoint.
- **WHEN** an unknown `DOCKYARP_LB` value is set **THEN** it falls back to the default with a warning
  (existing behavior preserved).

## Notes / risks / references
- nginx `ip_hash` ≠ YARP affinity exactly; document the mapping and any behavioral differences.
