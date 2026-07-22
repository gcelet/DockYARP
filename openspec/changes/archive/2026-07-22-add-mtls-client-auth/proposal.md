## Why

nginx-proxy supports mutual TLS per vhost (`ssl_verify_client` with a mounted `ca.crt`): clients must
present a certificate signed by a trusted CA. DockYarp has no client-certificate authentication at all.

## What Changes

- **CA validation at the handshake**: a configured client CA (`Tls:ClientCaCertificatePath`) makes Kestrel
  request a client certificate and validate that it chains to the CA (invalid certificates are rejected).
- **Per-host requirement**: a `DOCKYARP_CLIENT_CERT` label (`required`/`optional`/`none`) sets the
  route's client-certificate requirement; a middleware enforces it per host — a `required` route without a
  (valid, CA-chained) client certificate is rejected with 403.

Kestrel's `ClientCertificateMode` is per-endpoint, so validation is global (against the CA) and the
per-vhost requirement is enforced at the application layer.

## Capabilities

### Modified Capabilities
- `proxy-routing`: a route carries a client-certificate requirement.
- `docker-discovery`: `DOCKYARP_CLIENT_CERT` sets that requirement.
- `tls-acme`: a configured client CA makes Kestrel request and validate client certificates.
- `security`: a `required` route rejects requests without a valid client certificate.

## Impact

- **Code**: `src/DockYarp.Core` (`ClientCertificateRequirement`, `RouteRule`), `src/DockYarp.Docker`
  (label), `src/DockYarp.Tls` (`ClientCertificateValidator`, `TlsOptions`, `KestrelTlsConfigurator`),
  `src/DockYarp.Security` (enforcement middleware).
- **Lower test confidence**: the real handshake validation runs only at runtime; CA validation and per-host
  enforcement are unit-tested with generated certificates. Per-SNI-host handshake modes and CRL/OCSP
  revocation remain out of scope.
- **Owning agent**: AG-AT / AG-SEC / AG-DD.
