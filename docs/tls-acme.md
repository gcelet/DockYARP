# TLS & ACME (DockYarp.Tls)

DockYarp obtains and serves HTTPS certificates automatically for hosts that declare TLS metadata
(`LETSENCRYPT_HOST`/`LETSENCRYPT_EMAIL`, surfaced as `HostTlsMetadata` in the routing model).

## Components

| Type | Role |
|---|---|
| `ICertificateStore` / `FileCertificateStore` | PEM files under a directory (also reads legacy/operator-provided PFX), mirrored in memory for SNI. |
| `DefaultCertificateProvider` | Self-signed **fallback** certificate generated at startup. |
| `SniCertificateSelector` | Picks the exact host certificate, then a wildcard parent, else the fallback. |
| `KestrelTlsConfigurator` | Wires the selector into Kestrel's HTTPS defaults. |
| `IHttp01ChallengeStore` / `Http01ChallengeMiddleware` | Serves `/.well-known/acme-challenge/{token}`. |
| `IAcmeClient` / `CertesAcmeClient` | Obtains a certificate via ACME HTTP-01 or DNS-01 (Certes). |
| `IDnsChallengeProvider` / `Rfc2136DnsChallengeProvider` | Publishes/removes the `_acme-challenge` TXT record for DNS-01, via RFC 2136 (Dynamic DNS Update). |
| `CertificateProvisioningService` | Acquires missing certificates and renews near-expiry ones. |

## Flow

```
proxy-routing (HostTlsMetadata) ──> TlsDomains.Desired ──> CertificateProvisioningService
                                                                │  (missing or near expiry)
                                                                ▼
                                                    IAcmeClient (HTTP-01 default, DNS-01 opt-in)
                                                     │ HTTP-01                    │ DNS-01
                                                     ▼                            ▼
                                        Http01ChallengeMiddleware        IDnsChallengeProvider
                                          answers the token             publishes the TXT record
                                                     │                            │
                                                     └──────────────┬─────────────┘
                                                                     ▼
                                                          ICertificateStore.Save ──> SniCertificateSelector (hot reload)
```

The ACME challenge middleware runs **before** HTTPS enforcement so validation is reachable over HTTP.

The host listens for HTTPS on port 8443 (`ASPNETCORE_HTTPS_PORTS`); Kestrel selects the certificate per
SNI host via `KestrelTlsConfigurator`, with the self-signed fallback set as the default certificate (a
ports-only HTTPS endpoint requires a default certificate to start).

## Provided certificates & wildcard selection

ACME-issued certificates are persisted as a PEM pair (`{host}.crt` for the full chain, `{host}.key` for the
private key) — the same format an operator provides. Operators can **mount their own certificates** into
`CertificateDirectory` (loaded at startup, alongside ACME-persisted ones):

- **PEM pair**: `{host}.crt` + `{host}.key` (a `.crt` with no matching `.key` is skipped).
- **PFX**: `{host}.pfx` — still read for backward compatibility (e.g. a certificate persisted by an older
  DockYarp version), but a `{host}.crt`/`{host}.key` pair for the same host always takes precedence.

A mounted certificate takes precedence over an ACME-persisted one for the same host. Files are keyed by the
host in the file name. For a **wildcard** certificate (`*.example.com`), provide it under its base domain
(`example.com.crt`/`example.com.key`): SNI selection tries the exact host, then the parent domain (leftmost
label stripped), then the self-signed fallback. Filesystem access goes through `System.IO.Abstractions`,
so loading is unit-tested against a mock filesystem.

A wildcard `LETSENCRYPT_HOST` can also be **ACME-issued**, not just operator-provided — see
[DNS-01 & wildcard certificates](#dns-01--wildcard-certificates) below. A wildcard order is stored under its
base domain exactly like a mounted wildcard certificate, so the same SNI lookup serves either.

## Testing boundary

The real ACME exchange with the CA cannot run in the test suite (no CA). It lives behind `IAcmeClient`;
tests drive the whole orchestration (acquire → store → select; renew) with a fake client, so only the
Certes↔CA network exchange is untested (validate manually against Let's Encrypt **staging** / Pebble).

## Configuration (`TlsOptions`)

- `CertificateDirectory` — where certificate PEM files live (default `certs`; mount `/certs` in the container).
- `ContactEmail` — default ACME contact (per-host `LETSENCRYPT_EMAIL` overrides).
- `AcmeDirectoryUri` — defaults to **Let's Encrypt staging** to avoid rate limits.
- `AcceptTermsOfService`, `RenewBeforeExpiry` (default 30 days), `CheckInterval` (default 12 h).
- `MinimumTlsVersion` — `Tls12` (default; also enables TLS 1.3) or `Tls13` (1.3 only).
- `HttpProtocols` — enabled HTTP protocols (default `Http1AndHttp2`; e.g. `Http1AndHttp2AndHttp3` needs MsQuic).
- `CipherSuites` — optional cipher-suite allow-list, applied on Linux/macOS (ignored where the platform
  manages ciphers, e.g. Windows).

There is deliberately no DH-parameter (`DHPARAM_*`) equivalent: DH-group selection isn't exposed at the
application level by `SslServerAuthenticationOptions`/`CipherSuitesPolicy` on any platform — on Windows it's
entirely OS-managed (SChannel, via Group Policy/the `TLS` PowerShell module), and on Linux/macOS
`CipherSuitesPolicy` restricts which negotiated cipher suites are allowed without exposing the underlying
DH-group parameters. TLS 1.3 (DockYarp's default) doesn't use classic DH parameters at all.

A host whose `HTTPS_METHOD` is `nohttps` is **not** provisioned (it is served over HTTP only).

### DNS-01 & wildcard certificates

`DOCKYARP_ACME_CHALLENGE` selects the challenge type per host: `http-01` (default) or `dns-01` (case
insensitive; an unrecognized value falls back to `http-01` with a logged warning). DNS-01 is the **only**
way to issue a **wildcard** certificate (`LETSENCRYPT_HOST=*.example.com`) — this is an ACME protocol
constraint (a wildcard identifier is never valid for HTTP-01), not a DockYarp-specific restriction. A
wildcard order requests exactly that one identifier; the issued certificate is stored under the parent
domain (`example.com`), so it serves any subdomain via the existing wildcard SNI fallback — there is no
implicit certificate for the bare parent domain itself (declare a separate host for that if needed).

DockYarp's only DNS-01 provider today is **RFC 2136** (Dynamic DNS Update) — the generic mechanism most
ACME clients (cert-manager, Traefik, Certbot, Posh-ACME) use to talk to a self-hosted authoritative DNS
server (BIND, PowerDNS, CoreDNS, Technitium, ...) over a TSIG-authenticated update, not a commercial API.
It was chosen specifically because it needs no third-party account of any kind. Configure it globally
(`TlsOptions`, not per-host — DNS infrastructure is an operator-level concern):

- `DnsUpdateServer` — the RFC 2136 server, `host` or `host:port` (default port 53).
- `DnsUpdateZone` — the zone apex the update targets (e.g. `example.com`).
- `DnsUpdateTsigKeyName` — the TSIG key name configured on the DNS server.
- `DnsUpdateTsigKeySecret` — the TSIG key secret, base64-encoded.
- `DnsUpdateTsigAlgorithm` — the TSIG algorithm (default `hmac-sha256`; also `hmac-sha1`/`hmac-sha384`/`hmac-sha512`).

All four `DnsUpdateServer`/`DnsUpdateZone`/`DnsUpdateTsigKeyName`/`DnsUpdateTsigKeySecret` are required for
any `dns-01` host — if incomplete, provisioning fails for that host alone (a clear, actionable error) while
every other host (HTTP-01 or a correctly-configured DNS-01 one) is unaffected. The DNS UPDATE (RFC 2136 §2)
and TSIG (RFC 8945) wire formats are implemented directly against the BCL (no third-party DNS library) —
the one candidate NuGet package pulls in a `BouncyCastle.Cryptography` dependency that conflicts with
`Portable.BouncyCastle`, already used for CRL parsing.

### Mutual TLS

`ClientCaCertificatePath` points to a PEM CA bundle; `ClientCrlPath` an optional CRL checked alongside it
(`ClientCertificateValidator`, loaded via `System.IO.Abstractions`). Kestrel's request for a client certificate
is **per host** (`HostClientCertificateResolver`, resolved at handshake time from `RouteRule.ClientCertificate`
— `DOCKYARP_CLIENT_CERT`): a `required` host uses a strict callback that fails the handshake for a
missing-and-required, untrusted, or revoked certificate; an `optional` host uses a permissive callback that
never fails the handshake on the certificate's trust outcome; a `none` host is not prompted at all.
`ClientCertificateMiddleware` (re-)computes the verification status (`NotPresented`/`Verified`/`Failed`) for
`required`/`optional` routes and stores it on `HttpContext.Items` — a `required` route with a non-`Verified`
status is rejected with 403 (unreachable in practice for an invalid/revoked cert, already stopped at the
handshake); `ForwardedHeadersTransform` forwards it to the backend as `X-SSL-Client-Verify:
SUCCESS`/`FAILED`/`NONE` (subject/issuer only for `SUCCESS`).

## Deferred

A commercial DNS-01 provider (Cloudflare, Route53, ...) behind `IDnsChallengeProvider` — the abstraction is
designed to accept one without disruption, none ships today (RFC 2136 is the only provider); wiring the
store into `/api/certs`; switching HTTPS enforcement from the `EnforceHttps` flag to a real
cert-availability check.
