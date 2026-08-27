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
| `IAcmeClient` / `AcmeClient` | Obtains a certificate via ACME HTTP-01 or DNS-01 (hand-rolled RFC 8555 client — see [Client maintenance & security](#client-maintenance--security)). |
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
`AcmeClient`↔CA network exchange is untested at the unit level (proven instead by the e2e suite against a
real step-ca — see [Client maintenance & security](#client-maintenance--security)).

## Client maintenance & security

`AcmeClient` (`src/DockYarp.Tls/AcmeClient.cs` + `src/DockYarp.Tls/Acme/`) is a hand-rolled ACME v2 (RFC 8555)
client, not a NuGet package — a deliberate exception to this project's default preference for a maintained
package over self-maintained protocol code. It replaced Certes (`investigate-certes-aot-alternative`,
archived at `openspec/changes/archive/2026-08-25-investigate-certes-aot-alternative/`, full research there)
specifically because no candidate at the time was both AOT-clean and trustworthy enough for a TLS-critical
dependency — not because hand-rolling is preferred in general.

**Real scope**: single-host orders, HTTP-01 and DNS-01 challenges, ES256 only, one persisted ACME account per
(contact email, ACME directory endpoint) pair — see "Persisted ACME account" below. Verified end-to-end
against real step-ca via the e2e suite (`TlsTests.cs`), not just unit tests — a hand-rolled protocol client's
real bugs (e.g. the `Content-Type` charset RFC 8555 §6.2 rejects, found this way) surface against a live
server, not structural unit tests alone.

**Persisted ACME account** (`add-acme-account-persistence`, resolved): the account key is generated once per
(contact email, ACME directory endpoint) pair — not per request — and persisted at
`{CertificateDirectory}/acme/{email}/{directory-host}/{directory-path}/account.key` (an EC P-256 PEM),
reused for every subsequent certificate request/renewal sharing that pair, relying on RFC 8555 `newAccount`
idempotency. Scoping by contact email (not just by CA endpoint) matters: DockYarp already supports a
per-host `LETSENCRYPT_EMAIL`, and `newAccount` resolves an account by JWK, not by the request's `Contact`
field — so a host declaring a different email than another host still gets its own independent account,
unchanged from before persistence. An operator migrating an existing **EC (P-256)**-keyed
nginx-proxy/acme-companion account can place that account's PEM key at the resolved path before DockYarp's
first request for that (email, endpoint) pair to continue using it. **RSA-keyed account import is not
supported** — acme.sh's own default account-key algorithm when no EC key length is explicitly requested at
registration — since that would require adding RS256 (or general JWS-algorithm-negotiation) support to
`AcmeHttpClient`, which DockYarp's ES256-only signing doesn't have today; DockYarp fails clearly (identifying
the unsupported algorithm) rather than silently generating a new account when it finds one.

**Real known gaps**, from a completeness audit against RFC 8555 (not guessed), ranked by real severity for
DockYarp's actual goal — a transparent nginx-proxy replacement, where **Let's Encrypt, not step-ca, is the
realistic default CA** for most operators (this doc's own first assessment of the gaps below under-weighted
that; corrected):
- **Certificate revocation (§7.6) is not implemented** — no automated ACME-based path to revoke a certificate
  if its private key were compromised. Tracked as its own item, `add-acme-certificate-revocation`.
- **No `Retry-After`-aware backoff** on rate-limit or other transient errors — only `badNonce` (§6.7) gets a
  bounded retry today. Low-risk against step-ca, but a real gap against Let's Encrypt's own rate limits for
  the same reason as account persistence above. Tracked as its own item, `add-acme-retry-after-backoff`.
- **Now that an account persists across renewals, these become real (not just theoretical) opportunities —
  none implemented, no operator-facing need identified yet**: account update/deactivation (§7.3.2/§7.3.6),
  account key rollover (§7.3.5), pre-authorization (§7.4.1), and reusing an already-`valid` authorization from
  a prior order (RFC 8555 §7.5 — deliberately out of scope for `add-acme-account-persistence`, a fresh
  challenge is still requested every time). Revisit only if a real need surfaces.
- TLS-ALPN-01 challenge type: not implemented, not needed (DockYarp doesn't offer that challenge path).
- ACME Renewal Info (ARI, a newer draft extension beyond core RFC 8555): not implemented — a future-watch
  item, not a current gap (Certes itself predates ARI too).

**When to reconsider a NuGet package**: re-check the ecosystem (at minimum, the 3 forks and 2 further leads
already investigated in the archived research above — don't assume the landscape is unchanged) if any of the
following happens:
- A maintained, AOT-clean ACME client package appears or an existing one closes its Newtonsoft.Json gap.
- A security advisory affects ACME client implementations generally (JWS/nonce handling, TLS validation, ...).
- DockYarp's own requirements grow beyond the current scope (e.g. certificate revocation becomes a real need).

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
