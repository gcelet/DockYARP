## Context

`DockYarp.Tls` implements a certificate store, SNI selector (`KestrelTlsConfigurator` sets
`ConfigureHttpsDefaults(ServerCertificateSelector)`), ACME provisioning, and a self-signed fallback — but
the host never opens an HTTPS listener, so none of it serves. This change opens the listener.

## Goals / Non-Goals

**Goals:** serve HTTPS on a configurable port using per-SNI certificate selection with the fallback; keep
HTTP for ACME HTTP-01 and redirects; expose/publish HTTPS in the image and reference stack.

**Non-Goals:** `HTTPS_METHOD` modes, SSL policy/cipher tuning, mTLS (separate backlog changes).

## Decisions

- **Ports-only via `ASPNETCORE_HTTPS_PORTS=8443`** (the idiomatic .NET container knob), alongside the
  existing `ASPNETCORE_HTTP_PORTS=8080`. Per the Kestrel docs, a ports-only HTTPS endpoint **requires a
  default certificate**, otherwise the server fails to start.
- **Provide the default certificate in `ConfigureHttpsDefaults`**: set both `ServerCertificate` = the
  self-signed **fallback** (satisfies the startup requirement) and `ServerCertificateSelector` (per-SNI
  selection, overrides the default for known hosts). `KestrelTlsConfigurator` gains a
  `DefaultCertificateProvider` dependency (already registered).
- **Compose** maps `443:8443`; the `Dockerfile` already `EXPOSE`s 8443.
- **Verification is runtime** (WSL/Docker): `WebApplicationFactory` uses `TestServer` and does not bind
  Kestrel, so HTTPS binding is validated by running the container, not by the unit/integration suite.

## Risks / Trade-offs

- If the fallback default cert were missing, ports-only HTTPS would fail to start → mitigated by always
  providing it. [Risk: browsers distrust the self-signed fallback for unknown hosts] → expected; real hosts
  get ACME certs and are selected via SNI.

## Migration Plan

Additive: one env var, one compose port mapping, and a default cert on the existing HTTPS defaults.

## Open Questions

- Whether to make the HTTP/HTTPS ports first-class `TlsOptions` instead of env — deferred; env is standard.
