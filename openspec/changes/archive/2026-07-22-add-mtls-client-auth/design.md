## Context

DockYarp has no client-certificate authentication. nginx-proxy verifies client certs per vhost against a
mounted CA. Kestrel's `ClientCertificateMode` is per-endpoint (not per-SNI-host), so the CA validation is
global at the handshake and the per-vhost requirement is enforced in the application pipeline.

## Goals / Non-Goals

**Goals:** validate presented client certificates against a configured CA at the handshake; enforce a
per-route `required`/`optional`/`none` requirement (a `required` route with no client certificate → 403).

**Non-Goals (deferred):** per-SNI-host handshake modes, CRL/OCSP revocation checking, encrypted CA files,
and mapping the client identity to authorization.

## Decisions

- **`ClientCertificateRequirement` enum in Core.Models** (`None`, `Optional`, `Required`) as a first-class
  `RouteRule.ClientCertificate` field (like `Auth`) — not nested in `HostTlsMetadata`, so it is independent
  of ACME gating.
- **`DOCKYARP_CLIENT_CERT` label** parsed into `ContainerLabelConfig` (default `None`; `none`/`off` →
  `None`); a pure `LabelParser.HasUnsupportedClientCert` lets the mapper warn on an unrecognized value.
- **`ClientCertificateValidator` (Tls)** loads the CA (`TlsOptions.ClientCaCertificatePath`) via
  `IFileSystem` (PEM bundle → `X509Certificate2Collection`) and validates a certificate with an
  `X509Chain` using `CustomRootTrust` (offline, no revocation). `KestrelTlsConfigurator` sets
  `ClientCertificateMode.AllowCertificate` + a `ClientCertificateValidation` callback only when a CA is
  configured. The validator is pure/unit-tested; the Kestrel wiring is runtime-validated.
- **`ClientCertificateMiddleware` (Security)** looks up the route and, for a `Required` route with no
  `HttpContext.Connection.ClientCertificate`, returns 403. Added to the pipeline before Basic Auth.

## Risks / Trade-offs

- Global CA validation means any host may present a client cert, but only `required` routes enforce it —
  the coarse handshake mode is refined per host at the app layer, matching nginx-proxy's effect.
- Handshake behavior is validated only at runtime; unit tests cover CA chaining and per-route enforcement
  with generated certificates.

## Migration Plan

Additive: new enum + one `RouteRule` field (default `None`), a new option, a validator, and one middleware.
`KestrelTlsConfigurator` gains the validator dependency (updated with its test). Nothing changes unless a CA
is configured and a route sets the requirement.

## Open Questions

- Per-SNI-host client-cert modes and revocation — deferred; revisit with the runtime e2e harness.
