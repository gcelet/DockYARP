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

## Findings / scoping notes (2026-08-04 — NOT a clean Windows win; split before doing)
Investigated while triaging Track-3 clean wins. This item is **more involved than the stub implies** — read this
before proposing. Recommend splitting into two changes:

**A) CRL revocation — hard on .NET (defer / needs a dependency decision).**
- **No X509 CRL support in the BCL**: .NET (through .NET 10) has no public CRL reader *or* writer
  (`System.Security.Cryptography.X509Certificates` has no `X509Crl`). `X509Chain` cannot be handed a CRL file
  to check offline against a provided CRL.
- Checking a client cert against a supplied `<host>.crl.pem`/`ca.crl.pem` therefore needs **manual ASN.1
  parsing** (`System.Formats.Asn1`): walk `TBSCertList.revokedCertificates` → collect revoked serial numbers →
  test the client cert's serial. ~40–60 lines, fiddly.
- **Testing is the real blocker**: *generating* a test CRL also has no BCL API, so a unit test needs either a
  pre-baked CRL fixture (opaque) or **BouncyCastle** (`Portable.BouncyCastle`/`BouncyCastle.Cryptography`) — a
  new dependency. Decide dependency-vs-fixture before committing.
- Current state: `ClientCertificateValidator` (src/DockYarp.Tls) validates the client chain vs the CA with
  `X509RevocationMode.NoCheck`; CRL would extend it.

**B) Optional passthrough — touches handshake behavior (moderate).**
- mTLS is wired **globally**, not per-route: `SniTlsHandshakeCallback.BuildOptions` sets
  `ClientCertificateRequired = true` + one `RemoteCertificateValidationCallback` for *all* hosts whenever a
  client CA is configured (`mutualTls`). Per-host `optional`/`none` is **not** honored at the handshake — the
  callback rejects a missing/invalid cert connection-wide; `ClientCertificateMiddleware` only adds a per-host
  *presence* check (Required → 403).
- Non-blocking `optional` (nginx `ssl_verify_client=optional`) requires the handshake to **accept** a
  missing/invalid client cert for optional hosts (so the connection isn't dropped), then the middleware
  re-validates via `ClientCertificateValidator` and forwards the outcome to the backend as a header
  (`X-Client-Cert-Verify: SUCCESS|FAILED|NONE` + subject/issuer). That means making the handshake callback
  route/SNI-aware about the requirement, which the current global wiring does not do.
- This half **is** Windows-unit-testable (middleware + validator), but needs the handshake change first.

Suggested split: `add-mtls-optional-passthrough` (B, clean-ish) now-ish; `add-mtls-crl` (A) once the
BouncyCastle-vs-ASN.1 decision is made.

### Update 2026-08-14 — identity-passthrough half split off
The **identity** passthrough (forward `X-SSL-Client-Verify: SUCCESS` + subject/issuer for a *verified* client cert,
strip spoofed values) was split into **`add-mtls-optional-passthrough`** and implemented there. This item now retains:
(A) **CRL** revocation, and the harder part of (B) — the **non-blocking `optional_no_ca`** behavior (accept an
untrusted/absent cert at the handshake for `optional` hosts and forward `FAILED`/`NONE`), which needs the
`SniTlsHandshakeCallback` to become route/SNI-aware. Those remain deferred.
