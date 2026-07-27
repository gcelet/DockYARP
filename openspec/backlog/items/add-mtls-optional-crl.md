---
id: add-mtls-optional-crl
capability: security
agent: AG-SEC
tier: B-runtime
priority: medium
status: backlog
nginx-proxy: ssl_verify_client=optional / <host>.crl.pem / ca.crt
provenance: this parity pass (matrix: optional passthrough + CRL ⛔)
---

## Why
DockYarp's mTLS supports required/optional/none enforcement, but lacks two nginx-proxy capabilities:
non-blocking **optional** client-cert with status passthrough, and **CRL** revocation checking. Without CRL,
revoked client certs are still accepted; without optional-passthrough, apps can't make their own decision.

## nginx-proxy behavior
- `<host>.ca.crt` / global `ca.crt` enable client-cert auth; `<host>.crl.pem` / `ca.crl.pem` add revocation.
- Label `...ssl_verify_client=optional` makes the client cert non-blocking and exposes `$ssl_client_verify` to
  location snippets (default `on`).

## DockYarp today
`DOCKYARP_CLIENT_CERT` parses required/optional/none/off (`LabelParser.cs:141-147`); CA validation +
per-host enforcement in `src/DockYarp.Security/ClientCertificateMiddleware.cs` +
`src/DockYarp.Tls/ClientCertificateValidator.cs`. CRL is ⛔; the "optional" mode's downstream passthrough of
the verification result (a header for the backend) is not defined.

## Proposed change (sketch)
- CRL: load per-host/global CRLs and reject revoked client certs during validation.
- Optional passthrough: when `optional`, allow the request through and forward the verification outcome to the
  backend as a header (e.g. `X-Client-Cert-Verify: SUCCESS|FAILED|NONE`) plus subject/issuer.
- Also validate the handshake end-to-end (the e2e already exercises required mTLS).

## Acceptance criteria (→ scenarios)
- **WHEN** a client presents a cert revoked by the configured CRL **THEN** the request is rejected (403).
- **WHEN** `DOCKYARP_CLIENT_CERT=optional` and no cert is presented **THEN** the request proceeds and the
  backend receives a header indicating no client cert.
- **WHEN** `optional` and a valid cert is presented **THEN** the backend receives the verified subject.

## Notes / risks / references
- Define the exact passthrough header contract; confirm Kestrel client-cert negotiation for optional mode.
