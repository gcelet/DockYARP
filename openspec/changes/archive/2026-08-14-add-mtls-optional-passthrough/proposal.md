## Why
When a client presents a certificate to DockYarp's mTLS, TLS terminates at the proxy and the backend receives
**nothing** about the client — so it cannot authorize on the verified client identity (nginx-proxy forwards
`$ssl_client_verify` / `$ssl_client_s_dn`). This is the primary mTLS-passthrough use case and is missing today.

## What Changes
- The forwarded-header transform SHALL forward the client-certificate outcome to the backend when a client
  certificate is present on the connection:
  - `X-SSL-Client-Verify: SUCCESS`
  - `X-SSL-Client-S-DN` — the certificate subject
  - `X-SSL-Client-I-DN` — the certificate issuer
- It SHALL **always strip** any client-supplied `X-SSL-Client-*` headers first (anti-spoof), so the backend only ever
  sees DockYarp-set values and the absence of the header means "no verified client certificate".

Because DockYarp rejects an untrusted client certificate at the TLS handshake, a present certificate is a verified one
(`SUCCESS`); the non-blocking `optional_no_ca` **FAILED** passthrough and an explicit `NONE` value are the harder half
(a route/SNI-aware handshake) and stay deferred in `add-mtls-optional-crl`.

## Capabilities
### Modified Capabilities
- `yarp-dynamic-config`: forwarded headers include the verified client-certificate identity, and client-supplied
  `X-SSL-Client-*` values are stripped.

## Impact
- **Code**: `DockYarp.App/ReverseProxy/ForwardedHeadersTransform.cs` — strip + conditionally set the three headers in
  the existing request transform (same place as `X-Real-IP` / `X-Forwarded-Ssl`). No new configuration; automatic
  whenever a client cert is present (mTLS is already opt-in via `DOCKYARP_CLIENT_CERT` + a client CA).
- **Tests**: `DockYarp.IntegrationTests` — a spoofed `X-SSL-Client-*` is stripped and, with no client cert, no such
  header reaches the backend; `DockYarp.E2E.Tests` — the existing mTLS scenario (`mtls.local`) asserts the backend
  receives `X-SSL-Client-Verify: SUCCESS` and the subject over the real handshake.
- **Docs (user-facing — backend-visible headers)**: docs site `features.md` notes the client-certificate passthrough
  headers.
- **Out of scope / deferred** (`add-mtls-optional-crl`): CRL revocation, and non-blocking `optional_no_ca` (accept an
  untrusted/absent cert and forward `FAILED`/`NONE`). Owning agent: AG-SEC.
