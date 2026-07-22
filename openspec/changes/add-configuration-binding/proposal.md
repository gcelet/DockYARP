## Why

Several options are hard-coded with defaults in `Program.cs` instead of being bound from configuration:
`TlsOptions.AcmeDirectoryUri` (stuck on staging) and `AcceptTermsOfService`, `SecurityHeadersOptions`, the
Docker endpoint, etc. Production ACME issuance and tuning are therefore impossible without code changes.

## What Changes

- Bind options from configuration (appsettings/env), each under a clear section:
  - `Tls` → `TlsOptions` (`AcmeDirectoryUri`, `AcceptTermsOfService`, `ContactEmail`, `CertificateDirectory`, renewal margins).
  - `Security` → `SecurityHeadersOptions` (HSTS, headers).
  - `Docker` → discovery endpoint (+ existing `Docker:Enabled`).
  - `AdminApi` → `ApiKey`.
  - `Host` → shutdown timeout (already partly bound).
- Provide sensible, safe defaults (ACME staging remains the default until explicitly overridden).

## Capabilities

### Modified Capabilities
- `deployment`: the host binds its options from configuration.

## Impact

- **Code**: `src/DockYarp.App/Program.cs` (option binding), `appsettings.json` (documented keys).
- **Owning agent**: AG-DEP.
