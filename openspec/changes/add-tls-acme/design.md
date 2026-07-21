## Context

DockYarp needs automatic HTTPS for dynamically discovered hosts. The ACME protocol exchange with a CA
(Let's Encrypt) cannot run in the unit/integration suite (no CA), so the design isolates that exchange
behind a seam and makes everything else testable. `DockYarp.Tls` references `Core` and the ASP.NET
shared framework (Kestrel options + middleware + hosted service).

## Goals / Non-Goals

**Goals:**
- File-based certificate store loaded at startup, writable at runtime.
- Kestrel SNI selection with a generated self-signed fallback.
- HTTP-01 challenge store + middleware.
- ACME acquisition + renewal orchestration driven by the routing store's TLS metadata.

**Non-Goals:**
- No DNS-01. No `/api/certs` wiring (admin capability, later). HTTPS enforcement stays flag-based.
- The real Certes↔CA exchange is integration-only, not unit-tested.

## Decisions

- **`IAcmeClient` seam**: `Task<X509Certificate2> RequestCertificateAsync(host, email, ct)`. The
  provisioning service depends on it; tests use a `FakeAcmeClient` returning a self-signed cert, so the
  full orchestration (acquire → store → select; renew) is testable. `CertesAcmeClient` is the real adapter.
- **`ICertificateStore` / `FileCertificateStore`**: PFX files under a configured directory, filename per
  host, mirrored in a thread-safe in-memory map. Loaded via `X509CertificateLoader` (avoids the obsolete
  `X509Certificate2` file constructor). `Save` writes the PFX and updates the map → hot reload for SNI.
- **SNI selector**: `KestrelTlsConfigurator : IConfigureOptions<KestrelServerOptions>` sets
  `ConfigureHttpsDefaults(h => h.ServerCertificateSelector = ...)`; the selector returns the host cert or
  the fallback. Selection is a pure lookup, unit-tested directly (no real TLS in tests).
- **Self-signed fallback** generated at startup (`CertificateRequest` + RSA); never null so handshakes for
  unknown hosts complete deterministically.
- **HTTP-01**: `IHttp01ChallengeStore` (token → key-authorization) + `Http01ChallengeMiddleware` serving
  `/.well-known/acme-challenge/{token}`. The middleware runs **before** the security pipeline so ACME
  validation is reachable over HTTP even for enforced hosts.
- **Provisioning/renewal**: a `BackgroundService` reconciles on start, on store `Changed`, and on a timer:
  derive desired hosts from `HostTlsMetadata`; for each host missing a cert or within the renewal margin,
  call `IAcmeClient` and `Save`.

## Risks / Trade-offs

- ACME exchange untestable here → mitigated by the seam + fake; real path validated against Let's Encrypt
  staging / Pebble manually. Default directory is **staging** to avoid rate limits.
- Certes API surface: the adapter is verified to compile against the pinned Certes version; it is not run
  in tests.
- PFX stored without a password (files live in a protected volume). Documented; a password option can be
  added later.

## Migration Plan

Additive. Adds `Certes` (CPM) and an ASP.NET framework reference to `DockYarp.Tls`. Kestrel HTTPS
endpoints and the `/certs` volume are configured at deployment (`add-deployment`).

## Open Questions

- Whether HTTPS enforcement should switch from the flag to real cert-availability once the store exists —
  deferred to avoid modifying the archived `security` capability now.
