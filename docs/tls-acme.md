# TLS & ACME (DockYarp.Tls)

DockYarp obtains and serves HTTPS certificates automatically for hosts that declare TLS metadata
(`LETSENCRYPT_HOST`/`LETSENCRYPT_EMAIL`, surfaced as `HostTlsMetadata` in the routing model).

## Components

| Type | Role |
|---|---|
| `ICertificateStore` / `FileCertificateStore` | PFX and PEM files under a directory, mirrored in memory for SNI. |
| `DefaultCertificateProvider` | Self-signed **fallback** certificate generated at startup. |
| `SniCertificateSelector` | Picks the exact host certificate, then a wildcard parent, else the fallback. |
| `KestrelTlsConfigurator` | Wires the selector into Kestrel's HTTPS defaults. |
| `IHttp01ChallengeStore` / `Http01ChallengeMiddleware` | Serves `/.well-known/acme-challenge/{token}`. |
| `IAcmeClient` / `CertesAcmeClient` | Obtains a certificate via ACME HTTP-01 (Certes). |
| `CertificateProvisioningService` | Acquires missing certificates and renews near-expiry ones. |

## Flow

```
proxy-routing (HostTlsMetadata) ──> TlsDomains.Desired ──> CertificateProvisioningService
                                                                │  (missing or near expiry)
                                                                ▼
                                                          IAcmeClient (HTTP-01)
                                                                │  ┌── Http01ChallengeMiddleware answers the token
                                                                ▼  ┘
                                                          ICertificateStore.Save ──> SniCertificateSelector (hot reload)
```

The ACME challenge middleware runs **before** HTTPS enforcement so validation is reachable over HTTP.

The host listens for HTTPS on port 8443 (`ASPNETCORE_HTTPS_PORTS`); Kestrel selects the certificate per
SNI host via `KestrelTlsConfigurator`, with the self-signed fallback set as the default certificate (a
ports-only HTTPS endpoint requires a default certificate to start).

## Provided certificates & wildcard selection

Operators can **mount their own certificates** into `CertificateDirectory` (loaded at startup, alongside
ACME-persisted ones):

- **PEM pair**: `{host}.crt` + `{host}.key` (a `.crt` with no matching `.key` is skipped).
- **PFX**: `{host}.pfx`.

A mounted certificate takes precedence over an ACME-persisted one for the same host. Files are keyed by the
host in the file name. For a **wildcard** certificate (`*.example.com`), provide it under its base domain
(`example.com.crt`/`example.com.key`): SNI selection tries the exact host, then the parent domain (leftmost
label stripped), then the self-signed fallback. Filesystem access goes through `System.IO.Abstractions`,
so loading is unit-tested against a mock filesystem.

## Testing boundary

The real ACME exchange with the CA cannot run in the test suite (no CA). It lives behind `IAcmeClient`;
tests drive the whole orchestration (acquire → store → select; renew) with a fake client, so only the
Certes↔CA network exchange is untested (validate manually against Let's Encrypt **staging** / Pebble).

## Configuration (`TlsOptions`)

- `CertificateDirectory` — where PFX files live (default `certs`; mount `/certs` in the container).
- `ContactEmail` — default ACME contact (per-host `LETSENCRYPT_EMAIL` overrides).
- `AcmeDirectoryUri` — defaults to **Let's Encrypt staging** to avoid rate limits.
- `AcceptTermsOfService`, `RenewBeforeExpiry` (default 30 days), `CheckInterval` (default 12 h).
- `MinimumTlsVersion` — `Tls12` (default; also enables TLS 1.3) or `Tls13` (1.3 only).
- `HttpProtocols` — enabled HTTP protocols (default `Http1AndHttp2`; e.g. `Http1AndHttp2AndHttp3` needs MsQuic).
- `CipherSuites` — optional cipher-suite allow-list, applied on Linux/macOS (ignored where the platform
  manages ciphers, e.g. Windows).

A host whose `HTTPS_METHOD` is `nohttps` is **not** provisioned (it is served over HTTP only).

### Mutual TLS

`ClientCaCertificatePath` points to a PEM CA bundle. When set, Kestrel requests a client certificate and
accepts only those chaining to the CA (`ClientCertificateValidator`, loaded via `System.IO.Abstractions`).
The per-route requirement (`DOCKYARP_CLIENT_CERT` → `RouteRule.ClientCertificate`) is enforced by the
security layer: a `required` route with no client certificate is rejected with 403.

## Deferred

DNS-01 challenges; wiring the store into `/api/certs`; switching HTTPS enforcement from the `EnforceHttps`
flag to a real cert-availability check.
