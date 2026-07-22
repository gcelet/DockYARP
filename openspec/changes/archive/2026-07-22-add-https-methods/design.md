## Context

`HttpsRedirectionMiddleware` redirects HTTP→HTTPS whenever the matched route's `HostTlsMetadata.EnforceHttps`
is true — a bare flag with no certificate check, so it can redirect to an HTTPS endpoint that has no
certificate yet (before ACME completes). nginx-proxy instead offers `HTTPS_METHOD` and only redirects once a
cert exists. This replaces the flag with a method and gates redirection on real certificate availability.

## Goals / Non-Goals

**Goals:** model `HttpsMethod` (redirect/noredirect/nohttp/nohttps), parse `HTTPS_METHOD`, and redirect only
when the method is redirecting AND a certificate is available for the host.

**Non-Goals (deferred to `add-tls-hardening`):** refusing to serve a protocol at the listener level
(`nohttp`/`nohttps` beyond the redirect decision) and skipping ACME provisioning for `nohttps`.

## Decisions

- **`HttpsMethod` enum in Core.Models** (`Redirect` default, `NoRedirect`, `NoHttp`, `NoHttps`).
  `HostTlsMetadata.EnforceHttps` (bool) is **replaced** by `HttpsMethod Method` — one source of truth rather
  than a flag plus a method. Redirection applies for `Redirect`/`NoHttp`.
- **Label**: `HTTPS_METHOD` parsed into `ContainerLabelConfig.HttpsMethod` (default `Redirect`); a pure
  `LabelParser.HasUnsupportedHttpsMethod` lets `ContainerMapper` warn on an unrecognized value (parser stays
  pure). The method is carried on the `HostTlsMetadata` the mapper already builds for a certificate host.
- **Certificate-availability gate**: `HttpsRedirectionMiddleware` (in Security, which must not depend on
  Tls) takes a new `ICertificateAvailability` abstraction defined in Security; `DockYarp.App` provides
  `CertificateAvailabilityAdapter` over `ICertificateStore` (exact host, then wildcard parent — mirroring
  the SNI selector). Redirect only when `Redirects(method) && certificates.IsAvailable(host)`. This is the
  same dependency-inversion pattern used for admin observability.
- **Admin view**: `AdminApiModels.TlsView.EnforceHttps` (bool) becomes `HttpsMethod` (string) so `/api/routes`
  reflects the method.

## Risks / Trade-offs

- `nohttp`/`nohttps` are honored only for the redirect decision here (not protocol-level refusal); documented
  and deferred, so behavior is a safe subset (never worse than today).
- Availability is checked against the certificate store (exact + wildcard parent); a host served only by the
  self-signed fallback is treated as "no certificate", so it is not redirected — which is the desired fix.

## Migration Plan

Field replacement (`EnforceHttps` → `Method`) rippling to the Docker mapper, security middleware, admin view,
and their tests, all in this change. Default `Redirect` preserves today's behavior for ACME hosts (once a
cert exists); the only behavioral change is that redirection now waits for a certificate.

## Open Questions

- Listener-level protocol refusal and provisioning skip for `nohttps` — deferred to `add-tls-hardening`.
