## Context

Every e2e run reported "dockyarp failed to start". A standalone `docker run` of `dockyarp:local` with
the exact e2e environment (TLS env, `SSL_CERT_FILE`, client-CA path, the `/stepca` and `/clientca` mounts,
HTTPS on 8443) boots cleanly — so the app is fine and the fault is in how the AppHost declares the resource.
Two harness issues stacked up: a `WaitFor` on a never-healthy dependency, and container runtime args DCP
rejects.

## Goals / Non-Goals

**Goals:** DockYarp starts and becomes healthy in the e2e; discovery works; the HTTP scenarios can run.

**Non-Goals:** ACME HTTP-01 host resolution (step-ca → DockYarp), i.e. the ACME-cert-dependent TLS
scenarios; fixing step-ca's own health check; product changes (none needed).

## Decisions

- **Do not gate DockYarp on step-ca (`WaitFor` removed).** `CertificateProvisioningService` retries, so the
  proxy needs no CA at startup; gating only created a false dependency on step-ca's health check. This mirrors
  production, where a slow/absent CA must not stop the proxy serving HTTP. `WaitFor(dockerproxy)` stays — the
  socket proxy has no health check, resolves as soon as it runs, and discovery benefits from it.
- **Drop the `--network-alias` runtime args.** DCP failed to create the DockYarp container when
  `WithContainerRuntimeArgs("--network-alias", …)` was present (DockYarp was the only container missing
  from `docker ps -a`; the standalone `docker run` without these args worked). Remove them and the now-unused
  `BackendCatalog.TlsHosts`. The TLS env and CA mounts remain, so only HTTP-01 host resolution is missing.
- **Keep step-ca and the TLS wiring in the graph.** Everything else (ACME directory, CA trust, mutual-TLS
  client CA, HTTPS listener) stays, so the follow-up only needs a DCP-compatible host-resolution mechanism.

## Risks / Trade-offs

- Without HTTP-01 host resolution, ACME provisioning cannot complete, so the TLS scenarios that require a real
  certificate stay red until the follow-up. Fallback/mTLS scenarios that do not need a provisioned cert may
  already pass. This is expected and called out.
- DockYarp will log a few failed early ACME attempts (step-ca unreachable by the cert host); harmless.

## Migration Plan

Two removals in the test AppHost (`WaitFor(stepca)`, the alias args) plus the unused catalog member. No spec
or product behaviour change elsewhere.

## Open Questions

- The DCP-compatible way to make step-ca resolve `LETSENCRYPT_HOST` → DockYarp for HTTP-01 (a shared
  user-defined network with aliases, an `--add-host` on step-ca, or reusing DockYarp's own resource name as
  the cert host) — decided in the follow-up.
