# TLS & ACME (DockYarp.Tls)

DockYarp obtains and serves HTTPS certificates automatically for hosts that declare TLS metadata
(`LETSENCRYPT_HOST`/`LETSENCRYPT_EMAIL`, surfaced as `HostTlsMetadata` in the routing model).

## Components

| Type | Role |
|---|---|
| `ICertificateStore` / `FileCertificateStore` | PFX files under a directory, mirrored in memory for SNI. |
| `DefaultCertificateProvider` | Self-signed **fallback** certificate generated at startup. |
| `SniCertificateSelector` | Picks the host certificate or the fallback per SNI. |
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

## Testing boundary

The real ACME exchange with the CA cannot run in the test suite (no CA). It lives behind `IAcmeClient`;
tests drive the whole orchestration (acquire → store → select; renew) with a fake client, so only the
Certes↔CA network exchange is untested (validate manually against Let's Encrypt **staging** / Pebble).

## Configuration (`TlsOptions`)

- `CertificateDirectory` — where PFX files live (default `certs`; mount `/certs` in the container).
- `ContactEmail` — default ACME contact (per-host `LETSENCRYPT_EMAIL` overrides).
- `AcmeDirectoryUri` — defaults to **Let's Encrypt staging** to avoid rate limits.
- `AcceptTermsOfService`, `RenewBeforeExpiry` (default 30 days), `CheckInterval` (default 12 h).

## Deferred

DNS-01 challenges; wiring the store into `/api/certs`; switching HTTPS enforcement from the `EnforceHttps`
flag to a real cert-availability check.
