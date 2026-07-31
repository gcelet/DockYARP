---
id: add-session-affinity
capability: yarp-dynamic-config
agent: AG-RP
tier: B-runtime
priority: low
status: backlog
nginx-proxy: loadbalance label (ip_hash / hash $remote_addr)
provenance: split from add-loadbalance-policies, 2026-07-31
---

## Why
nginx-proxy's `loadbalance` label supports client-affinity (`ip_hash;`, `hash $remote_addr consistent;`) so a
client sticks to one upstream. YARP has no load-balancing *policy* for this; the equivalent is YARP **session
affinity** (cookie / custom-header). `add-loadbalance-policies` delivered the built-in policies and explicitly
left affinity here because it is cross-cutting (Data Protection).

## nginx-proxy behavior
- `com.github.nginx-proxy.nginx-proxy.loadbalance` can inject `ip_hash;` or a `hash` directive so requests from
  one client reach the same backend.

## DockYarp today
- Load-balancing policies (round-robin, least-requests, power-of-two-choices, random, first-alphabetical) are
  supported (`add-loadbalance-policies`). No client-affinity.

## Proposed change (sketch)
- Enable YARP **session affinity** per cluster (a `DOCKYARP_LB` affinity value or a dedicated label), mapping to
  YARP's affinity config (cookie by default; document that it is not IP-identical to nginx `ip_hash`).
- **Data Protection gate (hard requirement)**: YARP session affinity protects its affinity cookie with Data
  Protection — this is the **first real DP consumer**. Affinity MUST **require** the DP encryption certificate
  (`DataProtection:CertificatePath`) and **fail fast at startup** when it is absent, so a real payload is never
  protected by an unencrypted key ring. Today the benign `XmlKeyManager[35]` warning is suppressed precisely
  because there is no DP consumer; enabling affinity removes that justification, so revisit that suppression.

## Acceptance criteria (→ scenarios)
- **WHEN** affinity is enabled for a cluster **THEN** repeated requests from one client stick to one endpoint.
- **WHEN** affinity is enabled but `DataProtection:CertificatePath` is absent **THEN** the app fails fast at
  startup with a clear message (no unencrypted DP key ring protecting a real cookie).
- **WHEN** affinity is not enabled **THEN** behavior is unchanged (no DP requirement).

## Notes / risks / references
- nginx `ip_hash` ≠ YARP affinity exactly (cookie vs client-IP); document the difference.
- Sibling (done): `add-loadbalance-policies` (built-in policies). Ties into the deferred
  `secure-data-protection-keys` work.
- Runtime behavior (stickiness, fail-fast) needs an integration/e2e check; batch with the other e2e items.
