## Why

The `add-e2e-aspire` suite validates DockYarp end to end over **HTTP** only. The TLS-facing paths — ACME
certificate provisioning (HTTP-01), SNI selection, the self-signed fallback, HTTP→HTTPS redirection, per-host
HSTS, and mutual TLS — are still exercised by unit/integration tests alone, never against a live CA and a real
HTTPS handshake. This is the follow-up that closes that gap.

It adds a **step-ca** ACME server to the Aspire distributed system so DockYarp provisions **real
certificates** from a local CA over HTTP-01, then asserts the HTTPS behaviour from NUnit.

Because DockYarp's ACME client (Certes) uses the default `HttpClient` with **no CA override**, the proxy
container must trust the step-ca root at the OS level (`SSL_CERT_FILE`), and the test client must trust it
too. The change wires that trust at runtime from the CA step-ca generates at boot — **no CA key material is
committed to the repository**.

## What Changes

- Extend the Aspire AppHost with a `smallstep/step-ca` container exposing an ACME directory, and configure
  DockYarp for TLS: `Tls__AcmeDirectoryUri` (the step-ca ACME endpoint), `Tls__AcceptTermsOfService`,
  `Tls__ContactEmail`, `Tls__ClientCaCertificatePath` (for mTLS), the HTTPS endpoint (8443), and
  `SSL_CERT_FILE` pointing at the step-ca root so Certes trusts the CA.
- Add TLS-labeled backends: a host with `LETSENCRYPT_HOST` (gets an ACME cert), and a host additionally
  labeled `DOCKYARP_CLIENT_CERT=required` (mutual TLS). HTTP-only backends carry no `LETSENCRYPT_HOST`, so
  they get no cert and keep working unchanged (no redirect) alongside the TLS hosts.
- Add HTTPS scenarios to the e2e suite (all `[Category("EndToEnd")]`, still excluded by default): a real cert
  is provisioned for `LETSENCRYPT_HOST`; an unknown host falls back to the self-signed certificate;
  HTTP→HTTPS redirect; per-host HSTS; mutual TLS (valid client cert ⇒ 200, none ⇒ 403). The test client
  trusts the step-ca root read from the CA volume, and generates an ephemeral client CA for the mTLS case.

## Capabilities

### Modified Capabilities
- `deployment`: the end-to-end suite additionally covers TLS — ACME provisioning against a local CA, the
  HTTPS handshake, redirect/HSTS, and mutual TLS.

## Impact

- **Code**: `tests/DockYarp.E2E.AppHost` (step-ca container, DockYarp TLS env, CA trust, https endpoint,
  network aliases for HTTP-01), `tests/DockYarp.E2E.Tests` (HTTPS/mTLS scenarios + a TLS-aware client).
  No production code changes.
- **Runtime prerequisite**: unchanged — `E2E`/`Release` need a Docker daemon; the default build does not.
- **Runtime validation deferred**: like the HTTP batch, this is authored now and executed in a Docker-capable
  session. The CA-trust bridge, HTTP-01 host resolution (network aliases), and mutual-TLS client-cert wiring
  are the parts to validate at runtime.
- **Owning agent**: AG-AT (with AG-DEP for the harness).
