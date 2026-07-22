## Context

HTTPS runs on Kestrel defaults (`KestrelTlsConfigurator` only wires the SNI selector), HSTS is a single
global policy in `SecurityHeadersMiddleware`, and `nohttps` (from `add-https-methods`) is only honored as a
non-redirect — the host is still provisioned and served over HTTPS. This change wires TLS hardening knobs,
finishes `nohttps`, and adds HSTS preload + per-host override. Per the agreed scope, runtime-only aspects are
wired as configuration with defaults that preserve current behavior.

## Goals / Non-Goals

**Goals:** configurable minimum TLS version / HTTP protocols / cipher allow-list; `nohttps` excluded from
provisioning and refused over HTTPS; HSTS `preload` and per-host override.

**Non-Goals:** OCSP stapling, per-socket protocol refusal (single shared port), HTTP/3 beyond a config toggle
(needs MsQuic), and encrypted-key handling.

## Decisions

- **`TlsOptions`** gains `MinimumTlsVersion` (`TlsVersion` enum, default `Tls12`), `HttpProtocols` (string,
  default `Http1AndHttp2`), and `CipherSuites` (`string[]?`, default none). A pure `TlsHardening` helper maps
  the minimum version to `SslProtocols`, parses `HttpProtocols`, and parses cipher names to `TlsCipherSuite`
  (skipping unknowns) — all unit-tested. `KestrelTlsConfigurator` applies `SslProtocols` and the SNI selector
  in `ConfigureHttpsDefaults`, the cipher policy via `OnAuthenticate` **only** on Linux/macOS
  (`CipherSuitesPolicy` is unsupported elsewhere), and the protocols via `ConfigureEndpointDefaults`.
- **`nohttps`**: `TlsDomains.Desired` skips routes whose `Tls.Method` is `NoHttps`; `HttpsRedirectionMiddleware`
  refuses an HTTPS request (404) for a `NoHttps` route (it already skips redirection for it).
- **HSTS**: `SecurityHeadersOptions.HstsPreload` adds `; preload`. `HostTlsMetadata.Hsts` (string?) carries a
  per-host override (parsed from the `HSTS` label into `ContainerLabelConfig`); `SecurityHeadersMiddleware`
  becomes route-aware (via `RouteLookup`) and, on HTTPS responses, uses the matched route's override
  (`off`/empty ⇒ suppress, otherwise the literal value) or the global policy.

## Risks / Trade-offs

- Cipher/protocol/HTTP-3 wiring is validated only at runtime; defaults equal today's behavior, so a
  misconfiguration is opt-in. Cipher policy is silently ignored on Windows (schannel manages ciphers).
- `SecurityHeadersMiddleware` gains a `RouteLookup` dependency; unmatched requests fall back to the global
  HSTS policy (unchanged).

## Migration Plan

Additive options and one new model field; middleware/configurator constructors gain dependencies (updated in
this change and its tests). Defaults preserve behavior; only explicit configuration or the `nohttps`/`HSTS`
labels change anything.

## Open Questions

- OCSP stapling and true per-socket protocol disabling — deferred; revisit with the runtime e2e harness.
