---
id: e2e-ssl-policy-negotiation
capability: tls-acme
agent: AG-AT
tier: B-runtime
priority: low
status: backlog
nginx-proxy: SSL_POLICY (runtime negotiation validation)
provenance: deferred from add-ssl-policy-presets, 2026-07-30
---

## Why
`add-ssl-policy-presets` maps a named `SSL_POLICY` preset to a minimum TLS version + cipher list and unit-tests
the mapping, but does not prove that the running HTTPS endpoint actually *negotiates only* the preset's suites.
That is a live-handshake behavior and, like the existing cipher allow-list, is enforced only on Linux/macOS.

## nginx-proxy behavior
- `SSL_POLICY` selects a predefined protocol+cipher policy that the server then enforces during the TLS
  handshake.

## DockYarp today
- Preset → version/cipher mapping is implemented and unit-tested (`add-ssl-policy-presets`). No live handshake
  asserts that only the preset's protocols/suites are negotiable.

## Proposed change (sketch)
- In the Aspire e2e suite (Linux), start the proxy with `Tls:SslPolicy=Mozilla-Modern` and assert a TLS 1.2
  handshake is refused while a TLS 1.3 handshake succeeds; optionally assert a non-preset cipher is refused.
- Repeat for `Mozilla-Intermediate` (TLS 1.2 accepted).

## Acceptance criteria (→ scenarios)
- **WHEN** the endpoint runs with `Mozilla-Modern` **THEN** a TLS 1.2 client handshake fails and a TLS 1.3 one
  succeeds.
- **WHEN** the endpoint runs with `Mozilla-Intermediate` **THEN** a TLS 1.2 handshake succeeds.

## Notes / risks / references
- Cipher enforcement is Linux/macOS only; run this scenario on Linux (the e2e host), not Windows dev.
- Sibling (done): `add-ssl-policy-presets` (mapping).
