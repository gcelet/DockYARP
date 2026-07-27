---
id: add-ocsp-stapling
capability: tls-acme
agent: AG-AT
tier: B-runtime
priority: low
status: backlog
nginx-proxy: OCSP stapling via <host>.chain.pem
provenance: this parity pass (matrix: OCSP stapling ⛔)
---

## Why
nginx-proxy auto-enables OCSP stapling when a trusted-chain file (`<host>.chain.pem`) is present, improving
TLS handshake latency and revocation checking. DockYarp does not staple OCSP responses.

## nginx-proxy behavior
- When `/etc/nginx/certs/<host>.chain.pem` exists, nginx sets `ssl_stapling on` + `ssl_trusted_certificate`,
  stapling the CA's OCSP response.

## DockYarp today
Kestrel/SNI serves the leaf (+ provided chain) but does not fetch/staple OCSP responses (matrix ⛔). See the
Kestrel TLS wiring in `src/DockYarp.Tls/KestrelTlsConfigurator.cs`.

## Proposed change (sketch)
Investigate Kestrel/.NET OCSP-stapling support (it is limited/OS-dependent). If viable, fetch and cache the
OCSP response for certs that carry an AIA OCSP URL + trusted chain, and staple during the handshake; otherwise
document as an OS/runtime limitation.

## Acceptance criteria (→ scenarios)
- **WHEN** a cert with a reachable OCSP responder + trusted chain is served **THEN** the TLS handshake includes
  a stapled OCSP response.
- **WHEN** OCSP stapling is unsupported on the platform **THEN** startup logs the limitation and TLS still
  works.

## Notes / risks / references
- .NET/Kestrel OCSP stapling is historically constrained; validate feasibility before committing scope.
- Runtime-heavy; needs a Docker-capable validation.
