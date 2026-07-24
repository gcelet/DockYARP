## Why

The TLS e2e suite passes 19/21. The two remaining failures — `AcmeCertificate_IsProvisionedForHost` and
`HttpRequest_RedirectsToHttps` — both need a **real ACME certificate** for `tls.local`, which never gets
issued: for the ACME HTTP-01 challenge, step-ca fetches `http://<LETSENCRYPT_HOST>/.well-known/acme-challenge/…`
and must resolve `<LETSENCRYPT_HOST>` to DockYarp **on port 80**. Two obstacles:

1. **Name resolution** — under Aspire/DCP, containers only resolve each other by *resource name*; an arbitrary
   host like `tls.local` does not resolve. (A raw `--network-alias` runtime arg is rejected by DCP.)
2. **Port 80** — ACME HTTP-01 is fixed to port 80, but DockYarp runs **non-root** and listens on 8080; a
   non-root process cannot bind port 80.

## What Changes

Add a tiny **`socat` sidecar** to the e2e AppHost that solves both without touching DockYarp and without
any container runtime args:

- It carries the TLS virtual hosts as **native network aliases** (`WithContainerNetworkAlias("tls.local")`,
  …`hsts.local`, …`mtls.local`), so step-ca resolves those names to the sidecar.
- It listens on **port 80** (running as root inside its own throwaway container) and forwards to
  `dockyarp:8080`, so the ACME challenge reaches DockYarp's HTTP-01 endpoint.

step-ca then validates the challenge, issues the certificate, and DockYarp serves it over HTTPS. The tests
already connect to DockYarp's HTTPS endpoint directly (they do not use the alias), so nothing else changes.
DockYarp stays non-root on 8080/8443, unmodified.

Two more test-only adjustments make provisioning actually happen and be observed:

- **Short `Tls__CheckInterval` in the e2e AppHost.** The provisioning service reconciles once at startup and
  then on `CheckInterval` (default 12h). The startup pass races Docker discovery and reads an empty store, so
  no certificate is requested until 12h later — nothing is issued in the test window. A 5-second interval lets
  it retry right after discovery populates the store. (A live run confirmed the diagnosis: `tls.local` has a
  `tls.certificateHost` route but `/api/certs` is empty and no provisioning is even attempted.)
- **`AcmeCertificate_IsProvisionedForHost` polls for the ACME certificate**, not merely a `200`: otherwise it
  accepts the self-signed fallback served before provisioning completes.
- **Trust step-ca's full chain (root + intermediate).** With provisioning finally attempted, the ACME call
  failed with `PartialChain`: DockYarp's `SSL_CERT_FILE` held only the step-ca **root**, but step-ca does
  not send its intermediate, so the chain to the ACME endpoint could not be built. The harness now writes a
  root+intermediate **bundle** (once step-ca has emitted its PKI) and points `SSL_CERT_FILE` at it. (A live
  probe confirmed the HTTP-01 path itself is fine: step-ca reaches `tls.local` through the socat sidecar.)

## Capabilities

### Modified Capabilities
- `deployment`: the TLS e2e provisions real ACME certificates in-cluster (the ACME authority can reach
  DockYarp's HTTP-01 challenge endpoint), so the ACME-certificate and HTTP→HTTPS-redirect scenarios pass.

## Impact

- **Test harness only**: `tests/DockYarp.E2E.AppHost` (one `socat` container added). No product/`src` change,
  no change to the DockYarp resource.
- **Expected**: the e2e suite reaches 21/21.
- **Risk**: relies on `WithContainerNetworkAlias` working under DCP (the native, documented API — unlike the
  raw runtime arg that failed). Fallback if aliases still fail at runtime: revisit.
- **Owning agent**: AG-DEP (with AG-AT).
