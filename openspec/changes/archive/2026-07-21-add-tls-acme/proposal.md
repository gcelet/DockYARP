## Why

DockYarp must provide HTTPS automatically for discovered hosts, like nginx-proxy + its ACME companion:
obtain certificates, store them, renew before expiry, and serve them via SNI — all without a restart.

## What Changes

- Add a **file-based certificate store** (`/certs`) that loads existing certificates at startup and accepts
  new ones at runtime.
- Add a **Kestrel SNI certificate selector** that serves the right certificate per host and a generated
  **self-signed fallback** for hosts without one (so handshakes fail gracefully).
- Add an **HTTP-01 challenge** store + middleware (`/.well-known/acme-challenge/...`).
- Add an **`IAcmeClient` seam** (obtain a certificate for a host via HTTP-01) with a **Certes**
  implementation, plus a **provisioning/renewal background service** that requests certificates for hosts
  carrying TLS metadata and renews them before expiry.

## Capabilities

### New Capabilities
- `tls-acme`: certificate store, Kestrel SNI + fallback, HTTP-01 challenge, ACME client (Certes) behind a
  seam, and a provisioning/renewal service.

### Modified Capabilities
<!-- None. Reads HostTlsMetadata from proxy-routing; HTTPS enforcement keeps using the EnforceHttps flag. -->

## Impact

- **Code**: `src/DockYarp.Tls` (store, selector, fallback, challenge, `IAcmeClient` + Certes adapter,
  provisioning service, options); Kestrel + challenge wiring in `DockYarp.App`.
- **Dependencies**: `Certes` (CPM); `DockYarp.Tls` references the ASP.NET shared framework.
- **Testing**: the orchestration is tested with a fake `IAcmeClient`; the real Certes network exchange with
  the CA is integration-only (a live/Pebble ACME server), not unit-tested.
- **Deferred**: DNS-01 challenges; wiring the cert store into `/api/certs`; switching HTTPS enforcement to a
  real cert-availability check (stays flag-based for now).
- **Owning agent**: AG-AT.
