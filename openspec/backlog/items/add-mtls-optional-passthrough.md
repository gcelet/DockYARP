---
id: add-mtls-optional-passthrough
capability: yarp-dynamic-config
agent: AG-SEC
tier: B-runtime
priority: medium
nginx-proxy: ssl_client_verify / ssl_client_s_dn passthrough headers
provenance: 2026-08-14 split from add-mtls-optional-crl (the clean identity-passthrough half)
status: backlog
---

## Why
When a client presents a certificate to DockYarp's mTLS, TLS terminates at the proxy and the backend receives
**nothing** about the client identity — so a backend cannot authorize on the verified client (nginx-proxy forwards
`$ssl_client_verify` / `$ssl_client_s_dn`). This is the primary mTLS-passthrough use case.

## DockYarp today
mTLS validates the client cert at the TLS handshake (`ClientCertificateValidator`, chain-to-CA); an untrusted cert is
rejected at the handshake, and `ClientCertificateMiddleware` enforces `Required` (403 when absent). No client-cert
information is forwarded to the backend.

## Proposed change (sketch)
- In the forwarded-header transform (`ForwardedHeadersTransform`, App/ReverseProxy — same place as X-Forwarded-*):
  when a client certificate is present, forward `X-SSL-Client-Verify: SUCCESS`, `X-SSL-Client-S-DN` (subject), and
  `X-SSL-Client-I-DN` (issuer).
- **Always strip** any client-supplied `X-SSL-Client-*` headers first (anti-spoof), so the backend only ever sees
  DockYarp-set values; the header's absence means "no verified client certificate".

## Acceptance criteria (→ scenarios)
- **WHEN** a request arrives with a verified client certificate **THEN** the backend receives `X-SSL-Client-Verify:
  SUCCESS` plus the certificate subject and issuer.
- **WHEN** a request arrives with no client certificate **THEN** no `X-SSL-Client-*` header reaches the backend.
- **WHEN** a client supplies its own `X-SSL-Client-*` header **THEN** it is stripped (never forwarded as-is).

## Notes / risks / references
- Because DockYarp rejects an untrusted client cert at the TLS layer, the forwarded status is `SUCCESS` (present) or
  absent (none) — the non-blocking `optional_no_ca` **FAILED** passthrough and the explicit `NONE` signal are the
  harder half (route/SNI-aware handshake) and stay in [`add-mtls-optional-crl`](add-mtls-optional-crl.md).
- Unit-test the pure DN/header formatting; extend the existing mTLS e2e (`mtls.local`) to assert the backend sees the
  headers. Refs: `ForwardedHeadersTransform.cs`, `ClientCertificateMiddleware.cs`.
