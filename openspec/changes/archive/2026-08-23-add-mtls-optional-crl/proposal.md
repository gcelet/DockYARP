## Why

DockYarp's mTLS supports `required`/`optional`/`none` enforcement, but lacks two nginx-proxy capabilities: CRL
revocation checking (a revoked client cert is still accepted today) and true non-blocking `optional` behavior (an
untrusted/invalid client cert on an `optional` host currently drops the TLS connection outright, instead of
letting the app decide). Both gaps share the same root cause — the TLS handshake validates a client certificate
**globally**, the same way for every host, regardless of that host's own `required`/`optional`/`none` setting —
so this change (already split twice from a larger original stub; see `openspec/backlog/items/add-mtls-optional-crl.md`)
makes the handshake host-aware and adds CRL checking.

## What Changes

- **CRL revocation**: a new global `Tls:ClientCrlPath` option (mirroring the existing global-only
  `Tls:ClientCaCertificatePath` — CRL scope matches the CA's existing scope, not per-host, since DockYarp has no
  per-host client-CA mechanism to extend). `ClientCertificateValidator` loads it via BouncyCastle
  (`Portable.BouncyCastle`, already an indirect dependency via Certes — .NET's BCL has no `X509Crl` type through
  .NET 10) and rejects a client certificate whose serial number is revoked, alongside the existing CA-chain check.
- **Host-aware handshake**: a new `HostClientCertificateResolver` (mirroring `HostSslPolicyResolver`/
  `HostHttp2Resolver`) resolves a host's `ClientCertificateRequirement` at handshake time.
  `SniTlsHandshakeCallback.BuildOptions` now only requests a client certificate for hosts whose requirement is
  `Required` or `Optional` (a `None` host no longer prompts for one at all — a natural side effect of making the
  callback host-aware, closer to nginx's per-server-block `ssl_verify_client`). `Required` hosts keep today's
  strict validation (an invalid/revoked cert drops the connection); `Optional` hosts get a permissive callback
  that never fails the handshake — the connection proceeds regardless of trust/revocation outcome, deferring the
  actual verification decision to the app layer.
- **Verification status threading**: `ClientCertificateMiddleware` computes the full outcome (not-presented /
  verified / failed) via `ClientCertificateValidator` (now including the CRL check) and stores it on
  `HttpContext.Items` for mTLS-aware routes (`Required`/`Optional`); `Required` keeps its existing 403 behavior
  (now driven by the computed status rather than a bare null-check, functionally unchanged for `Required` since
  an invalid cert can never reach the app layer on a `Required` host).
- **Header contract extended**: `X-SSL-Client-Verify` becomes `SUCCESS`/`FAILED`/`NONE` (was: `SUCCESS` or absent
  entirely) for any route with a `Required`/`Optional` client-certificate requirement — closing the ambiguity
  between "no header because no cert" and "no header because the request never reached this code path" and
  matching nginx's `$ssl_client_verify` semantics more closely. `X-SSL-Client-S-DN`/`X-SSL-Client-I-DN` are only
  set for `SUCCESS` (an untrusted cert's claimed identity is never forwarded as if verified). Routes with no
  client-certificate requirement are unaffected — no `X-SSL-Client-*` header, as today.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `security`: "Client certificate enforcement" — the 403 rejection is now driven by a computed verification
  status (not-presented / verified / failed via CA-chain + CRL) instead of a bare presence check; behaviorally
  equivalent for `Required` (an invalid/revoked cert already never reaches the app layer there), but the
  requirement text needs to reflect CRL as a rejection reason and the new status vocabulary.
- `tls-acme`: "Client certificate CA validation" and "Per-connection TLS session assembly" — client-certificate
  validation/request becomes per-host (`required` strict / `optional` permissive / `none` not requested at all),
  replacing the current global-for-every-host behavior; CRL revocation added as a rejection reason alongside
  CA-chain validation.
- `yarp-dynamic-config`: "Forwarded headers" — `X-SSL-Client-Verify` gains an explicit `FAILED`/`NONE` value
  (was: `SUCCESS` or the header entirely absent) for any mTLS-aware route.

## Impact

- `src/DockYarp.Tls/ClientCertificateValidator.cs` — CRL loading + revocation check (BouncyCastle).
- `src/DockYarp.Tls/HostClientCertificateResolver.cs` (new) — per-host requirement lookup, mirroring
  `HostSslPolicyResolver`/`HostHttp2Resolver`.
- `src/DockYarp.Tls/SniTlsHandshakeCallback.cs` — host-aware `ClientCertificateRequired`/validation callback
  (strict for `Required`, permissive for `Optional`, none for `None`).
- `src/DockYarp.Tls/TlsOptions.cs` — new `ClientCrlPath` option.
- `src/DockYarp.Security/ClientCertificateMiddleware.cs` — computes and stores the verification status; `Security`
  gains a new `DockYarp.Tls` `ProjectReference` (reuses `ClientCertificateValidator`, no cycle — see design.md).
- `src/DockYarp.App/ReverseProxy/ForwardedHeadersTransform.cs` — reads the stored status instead of a bare
  presence check; emits `FAILED`/`NONE`.
- `Directory.Packages.props` — new `Portable.BouncyCastle` `PackageVersion` (pinning an existing transitive
  dependency, not adding a new one — see design.md's Decisions).
- `docs-site/content/en/docs/configuration.md` (or equivalent) — document `DOCKYARP_CLIENT_CERT=optional`'s
  non-blocking behavior and the new `Tls:ClientCrlPath` option, per this project's user-facing-change rule.
- `docs/labels-reference.md` — if `<host>.crl.pem`-style labels/env exist there already for client-cert config,
  keep in sync; otherwise no entry needed (global-only option, not a label).
