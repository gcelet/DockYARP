## 1. Passthrough headers (AG-SEC)
- [x] 1.1 `ForwardedHeadersTransform`: strip inbound `X-SSL-Client-Verify` / `X-SSL-Client-S-DN` / `X-SSL-Client-I-DN`
  (anti-spoof), then set them from `HttpContext.Connection.ClientCertificate` when present (`SUCCESS` + subject + issuer)

## 2. Tests
- [x] 2.1 `ForwardedHeadersIntegrationTests`: extend the echo backend to reflect `X-SSL-Client-Verify`/`-S-DN`; a
  spoofed client-supplied `X-SSL-Client-*` is stripped and, with no client cert, no such header reaches the backend
- [x] 2.2 `DockYarp.E2E.Tests` (`TlsTests`, `mtls.local` valid-cert scenario): the backend receives
  `X-SSL-Client-Verify: SUCCESS` and a non-empty `X-SSL-Client-S-DN` over the real handshake

## 3. Docs (AG-DOC — backend-visible headers)
- [x] 3.1 docs site `features.md`: note the client-certificate passthrough headers (`X-SSL-Client-Verify`/`-S-DN`/`-I-DN`)
- [x] 3.2 `docs/testing.md`: note the passthrough assertion on the mTLS e2e row

## 4. Verify (AG-SEC)
- [x] 4.1 Nuke `Test` gate green (unit + integration), warnings-as-errors clean
- [x] 4.2 E2E `mtls.local` green (passthrough headers observed); `artifacts/e2e-logs/` healthy
