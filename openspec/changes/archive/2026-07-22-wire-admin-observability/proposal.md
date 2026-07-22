## Why

Two admin endpoints are placeholders: `/api/certs` always returns an empty list and `/api/health` returns a
hard-coded `"Healthy"`. They should reflect real state to be useful for operators.

## What Changes

- Wire `/api/certs` to the certificate store (`ICertificateStore.List()`), returning host + expiry per
  stored certificate (still no secrets).
- Make `/api/health` reflect real state: Docker discovery connectivity (when enabled), stored certificate
  count, and active route/cluster counts, with an overall status that degrades when a dependency is down.

## Capabilities

### Modified Capabilities
- `admin-api`: certificate and health endpoints report real state.

## Impact

- **Code**: `src/DockYarp.AdminApi` (certs endpoint reads the store; health aggregates real signals);
  `src/DockYarp.App` wiring to expose the certificate store to the admin API.
- **Owning agent**: AG-AA.
