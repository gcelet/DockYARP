## Context

`CertesAcmeClient` builds `new AcmeContext(options.AcmeDirectoryUri)` and drives the HTTP-01 flow with the
default `HttpClient` — there is no CA override. So an end-to-end ACME test needs (a) a real CA reachable over
HTTPS, (b) that CA trusted by the DockYarp container's OS, and (c) HTTP-01 challenge validation working
across the container network. Kestrel serves HTTPS on 8443 with an SNI selector and a self-signed default;
mutual TLS is enabled globally when `Tls__ClientCaCertificatePath` is set, with per-host `required`
enforcement in the security pipeline. This change assembles those pieces on Aspire and asserts them.

## Goals / Non-Goals

**Goals:** real ACME provisioning from a local step-ca over HTTP-01; assert the HTTPS handshake serves the
provisioned cert; self-signed fallback for unknown hosts; HTTP→HTTPS redirect; per-host HSTS; mutual TLS
(valid client cert ⇒ 200, none ⇒ 403). Reuse the existing AppHost/test project.

**Non-Goals:** cipher-suite / HTTP-3 / min-TLS-version assertions (deferred); production ACME; committing any
CA key material.

## Decisions

- **One distributed system.** The step-ca container and TLS backends join the existing AppHost. HTTP-only
  backends have no `LETSENCRYPT_HOST` (so no cert, `ICertificateAvailability` false ⇒ no redirect) and keep
  passing unchanged, so HTTP and TLS scenarios coexist under one proxy instance.
- **CA generated at boot, trust wired at runtime.** step-ca initialises its PKI on first boot
  (`DOCKER_STEPCA_INIT_*`, ACME provisioner enabled) into a bind-mounted host directory. DockYarp mounts
  the step-ca root from that directory and sets `SSL_CERT_FILE` so Certes trusts the CA; the test client reads
  the same root to trust DockYarp's HTTPS. No CA fixtures are committed. Rejected: committing a fixed
  step-ca PKI (needs the `step` CLI and puts key material in git).
- **HTTP-01 host resolution via network aliases.** step-ca validates a challenge by fetching
  `http://<LETSENCRYPT_HOST>/.well-known/acme-challenge/...`, so `<LETSENCRYPT_HOST>` must resolve to the
  DockYarp container. DockYarp is given `--network-alias <host>` (via container runtime args) for each
  TLS host so step-ca reaches it on 8080.
- **Mutual TLS without step-issued client certs.** The test generates an **ephemeral client CA + client leaf
  in memory** (`CertificateRequest`), writes the CA public cert to a shared host path bind-mounted into
  DockYarp as `Tls__ClientCaCertificatePath`, and presents the client leaf. This keeps client-auth material
  out of git and off step-ca. The shared path is derived from `Path.GetTempPath()` — identical for the AppHost
  and the test because `Aspire.Hosting.Testing` hosts the AppHost in the test process.
- **TLS-aware test client.** Scenarios build an `HttpClient` from the resource's `https` endpoint URI with a
  `ServerCertificateCustomValidationCallback` that captures the presented server certificate, so assertions
  can check the subject/issuer (ACME-issued vs self-signed fallback).

## Risks / Trade-offs

- **CA-trust bridge + ordering** (top risk): step-ca must write its root before DockYarp starts
  (`WaitFor(step-ca)` + health check), and the bind-mounted root path must line up on both containers.
- **Network aliases / DNS**: whether `--network-alias` on the Aspire container is honoured, and whether
  step-ca resolves it, is validated at runtime (fallback: a shared user network + explicit alias).
- **Chiseled image trust**: `SSL_CERT_FILE` is respected by .NET/OpenSSL on Linux; no shell needed. Confirm
  the mounted root is a single PEM bundle Certes accepts.
- All of the above is validated in a Docker-capable session; the harness is authored and compiles now.

## Migration Plan

Additive: extends the AppHost and test project from `add-e2e-aspire`; new HTTPS scenarios are category-gated,
so the default `Test` is unaffected. No production code changes.

## Open Questions

- Exact step-ca init env for a non-interactive ACME provisioner (`DOCKER_STEPCA_INIT_ACME`, password
  handling) — pinned during runtime validation.
- Whether DockYarp needs the step-ca **intermediate** as well as the root in `SSL_CERT_FILE` for chain
  building — resolved at runtime.
