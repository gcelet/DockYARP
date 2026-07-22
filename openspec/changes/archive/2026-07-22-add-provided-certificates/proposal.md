## Why

nginx-proxy lets operators mount their own certificates (PEM `{host}.crt` + `{host}.key`) into the certs
directory, and serves a wildcard certificate for subdomains. DockYarp's `FileCertificateStore` only loads
`{host}.pfx` files and its SNI selector only matches the exact host — so user-provided PEM certificates are
ignored and a wildcard certificate never covers its subdomains.

## What Changes

- Load **provided certificates** from the certificate directory at startup: PEM pairs (`{host}.crt` +
  `{host}.key`) **and** `{host}.pfx` files, keyed by host (file name). ACME-persisted PFX keeps working; a
  mounted certificate takes precedence.
- **Wildcard parent selection**: when no exact certificate matches the SNI host, fall back to the
  parent-domain certificate (a `*.example.com` cert is provided as `example.com`), then to the self-signed
  fallback.
- Route the store's filesystem access through `System.IO.Abstractions` so loading is unit-testable.

## Capabilities

### Modified Capabilities
- `tls-acme`: the certificate store loads mounted PEM/PFX certificates and SNI selection matches a wildcard
  parent certificate.

## Impact

- **Code**: `src/DockYarp.Tls` (`FileCertificateStore` PEM+PFX loading via `IFileSystem`,
  `SniCertificateSelector` wildcard-parent lookup, DI registration of `IFileSystem`).
- **Dependencies**: `System.IO.Abstractions` (+ `.TestingHelpers` for tests) via CPM.
- **Deferred**: `CERT_NAME` (per-vhost shared certificate label), encrypted private keys.
- **Owning agent**: AG-AT.
