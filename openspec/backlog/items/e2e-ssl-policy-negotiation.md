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
- **Global** (`add-ssl-policy-presets`): start the proxy with `Tls:SslPolicy=Mozilla-Modern` and assert a TLS 1.2
  handshake is refused while a TLS 1.3 handshake succeeds; optionally assert a non-preset cipher is refused.
  Repeat for `Mozilla-Intermediate` (TLS 1.2 accepted).
- **Per-vhost** (`add-per-vhost-ssl-policy`): on a single proxy instance, run two certified backends whose
  containers set different `SSL_POLICY` values (e.g. host A `SSL_POLICY=Mozilla-Modern`, host B left on the
  global posture). Assert host A refuses a TLS 1.2 handshake while host B accepts it — proving the per-SNI policy
  override negotiates live.

## Acceptance criteria (→ scenarios)
- **WHEN** the endpoint runs with `Mozilla-Modern` **THEN** a TLS 1.2 client handshake fails and a TLS 1.3 one
  succeeds.
- **WHEN** the endpoint runs with `Mozilla-Intermediate` **THEN** a TLS 1.2 handshake succeeds.
- **WHEN** two hosts on one instance declare different `SSL_POLICY` values **THEN** each host's handshake
  enforces its own policy (the `Mozilla-Modern` host refuses TLS 1.2 while the global-posture host accepts it).

## Notes / risks / references
- Cipher enforcement is Linux/macOS only; run this scenario on Linux (the e2e host), not Windows dev.
- Siblings (done, unit-tested, parity ⚠️ until this e2e is green): `add-ssl-policy-presets` (global mapping),
  `add-per-vhost-ssl-policy` (per-host override). Green here flips **both** the global and per-vhost parity rows.
