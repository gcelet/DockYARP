## Why

The TLS stack (certificate store, SNI selector, ACME provisioning) is implemented, but the host never
configures an HTTPS listener — so nothing actually serves TLS. This wires Kestrel to listen on HTTPS using
the existing `SniCertificateSelector`, making automatic HTTPS real.

## What Changes

- Configure a **Kestrel HTTPS endpoint** (default port 8443) that selects certificates per SNI via the
  existing selector (no per-host endpoint configuration needed).
- **Expose and map** the HTTPS port in the `Dockerfile` and reference `docker-compose.yml`.
- Keep HTTP listening (for ACME HTTP-01 challenge and HTTP→HTTPS redirect).

## Capabilities

### Modified Capabilities
- `deployment`: the host serves HTTPS via an SNI-selected certificate on a configurable port.

## Impact

- **Code**: `src/DockYarp.App` (Kestrel HTTPS endpoint wiring), `Dockerfile`, `docker-compose.yml`.
- **Depends on**: `tls-acme` (SNI selector, fallback cert) — already implemented.
- **Owning agent**: AG-AT / AG-DEP.
