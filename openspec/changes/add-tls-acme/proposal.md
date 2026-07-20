## Why

DockYarp must provide HTTPS automatically for discovered hosts, like nginx-proxy + its ACME companion.
Certificates must be obtained, stored, renewed, and served by Kestrel without downtime.

> Status: **sketch** — proposal + spec intent only. Design and tasks to be detailed just-in-time when
> this phase starts.

## What Changes

- Add an ACME v2 client (HTTP-01 challenge by default) that obtains certificates for hosts carrying TLS
  metadata (`LETSENCRYPT_HOST`/`LETSENCRYPT_EMAIL`).
- Define a certificate storage layout (under a mounted `/certs`).
- Add a renewal scheduler (background job) that renews before expiry.
- Integrate certificates into Kestrel via SNI with hot reload (no restart when a cert is added/renewed).
- Provide a default/fallback certificate so TLS handshakes for unknown hosts fail gracefully.

## Capabilities

### New Capabilities
- `tls-acme`: ACME v2 client, certificate storage, renewal scheduler, Kestrel SNI integration with hot
  reload, and a default/fallback certificate.

### Modified Capabilities
<!-- None: reads per-host TLS metadata from proxy-routing. -->

## Impact

- **Code**: `src/DockYarp.Tls` + Kestrel configuration in `DockYarp.App`.
- **Dependencies**: an ACME library (e.g. `Certes`/`ACMESharpCore`) — decision recorded in design at phase start.
- **Upstream**: requires `add-proxy-routing-model` (TLS metadata). **Owning agent**: AG-AT.
