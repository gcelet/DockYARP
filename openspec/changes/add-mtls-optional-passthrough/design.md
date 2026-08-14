# Design — add-mtls-optional-passthrough

## Where
`ForwardedHeadersTransform.Apply` already registers a per-request `AddRequestTransform` that sets `X-Real-IP`,
`X-Forwarded-Port`, `X-Forwarded-Ssl`, `X-Original-URI`, and strips the `Proxy` header. The client-certificate
passthrough belongs in the same transform — it has `transformContext.HttpContext.Connection.ClientCertificate` (the
cert Kestrel captured at the handshake) and `transformContext.ProxyRequest.Headers` (the outbound request).

## Behavior
```csharp
// Anti-spoof: never forward a client-supplied client-cert header — strip first, set only from the connection.
foreach (string header in ClientCertificateHeaders)   // "X-SSL-Client-Verify", "-S-DN", "-I-DN"
{
    transformContext.ProxyRequest.Headers.Remove(header);
}

if (transformContext.HttpContext.Connection.ClientCertificate is { } clientCertificate)
{
    transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-SSL-Client-Verify", "SUCCESS");
    transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-SSL-Client-S-DN", clientCertificate.Subject);
    transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-SSL-Client-I-DN", clientCertificate.Issuer);
}
```
- **Present cert ⇒ verified** — DockYarp rejects an untrusted client cert at the handshake
  (`SniTlsHandshakeCallback` validation callback), so any cert reaching the pipeline chained to the CA. The forwarded
  status is therefore `SUCCESS`.
- **No cert ⇒ no header** — nothing is added; combined with the unconditional strip, the backend sees an
  `X-SSL-Client-*` header only when DockYarp itself verified a client certificate. Its absence is the "no client
  certificate" signal.
- `X509Certificate2.Subject` / `.Issuer` are the RFC 2253 distinguished names. `TryAddWithoutValidation` mirrors the
  sibling forwarded headers (no header-value validation surprises).

## Why here and not a new middleware
The value must land on the **outbound** (backend) request, which is a YARP transform concern. `ForwardedHeadersTransform`
is that seam and already owns the strip-and-set pattern (`Proxy`, `X-Forwarded-Ssl`). No new middleware, no per-request
service lookup, no route lookup — the connection's client certificate is sufficient.

## Not changed
- The handshake wiring and `ClientCertificateMiddleware` (Required → 403) are untouched — this change only forwards the
  outcome; enforcement is unchanged.
- No new config key; the headers appear whenever a client cert is present (mTLS is already opt-in per host).

## Tests
- **Integration** (`ForwardedHeadersIntegrationTests`, extend the echo backend to reflect `X-SSL-Client-Verify`/`-S-DN`):
  a client sends a spoofed `X-SSL-Client-Verify: SUCCESS` to a non-mTLS route ⇒ the backend receives it **stripped**
  (no client cert on the connection ⇒ no header). Proves the anti-spoof + absent path without needing real TLS.
- **E2E** (`TlsTests`, the `mtls.local` scenario with a valid client cert): the backend echo shows
  `X-SSL-Client-Verify: SUCCESS` and a non-empty `X-SSL-Client-S-DN` — the real verified-cert passthrough over the
  actual handshake. Update `docs/testing.md` (mTLS e2e row) to note the passthrough assertion.
