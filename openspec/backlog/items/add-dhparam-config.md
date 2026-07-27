---
id: add-dhparam-config
capability: tls-acme
agent: AG-AT
tier: B-runtime
priority: low
status: backlog
nginx-proxy: DHPARAM_BITS / DHPARAM_SKIP / <host>.dhparam.pem
provenance: this parity pass (matrix: DH params ⛔)
---

## Why
nginx-proxy ships and configures DH parameters (`DHPARAM_BITS`, per-vhost `<host>.dhparam.pem`, `DHPARAM_SKIP`)
for DHE cipher suites. DockYarp has no DH-param configuration. **Low value on modern stacks** (TLS 1.3 uses
ECDHE and ignores classic DH params), tracked for completeness.

## nginx-proxy behavior
- `DHPARAM_BITS` (2048/3072/4096, default 4096) selects the RFC7919 group; `DHPARAM_SKIP` disables default DH
  params; per-vhost `<host>.dhparam.pem` overrides globally.

## DockYarp today
No DH-param handling; Kestrel/SChannel/OpenSSL manage key exchange. TLS 1.3 (default) does not use these
params.

## Proposed change (sketch)
Assess whether Kestrel exposes any DH-param control (largely not on modern TLS). If there is a meaningful knob
for TLS 1.2 DHE, wire it; otherwise **close as documented non-applicable** and record the reasoning in
`docs/tls-acme.md`.

## Acceptance criteria (→ scenarios)
- **WHEN** the stack negotiates TLS 1.3 **THEN** DH-param config is irrelevant (documented).
- **WHEN** (if supported) a DHE cipher over TLS 1.2 is negotiated **THEN** the configured group size applies.

## Notes / risks / references
- Strong candidate to become a documented non-goal after feasibility check.
