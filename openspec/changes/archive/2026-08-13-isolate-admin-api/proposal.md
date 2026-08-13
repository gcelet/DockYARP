## Why
The Admin API and `/metrics` are mounted on the data-plane port, on **all hosts**, at fixed `/api/*` paths with
precedence over YARP's catch-all (`src/DockYarp.App/Program.cs`). So a proxied backend that itself exposes an admin
sub-path — notably the very common **`/api/health`**, or `/api/routes`, `/metrics`, … — is **shadowed**: the request
is answered by DockYarp (a `401` without the API key) instead of being proxied. This **blocks** running DockYarp in
front of real services that use `/api/*` routes.

## What Changes
- Add **`AdminApi:Host`**: when set, the admin endpoints (`/api/*` and `/metrics`) respond **only** to requests whose
  `Host` matches it (via `RequireHost`). On every other host those paths **fall through to normal proxying**, so a
  backend's `/api/health` etc. is no longer shadowed.
- When `AdminApi:Host` is **unset**, behavior is unchanged (admin on all hosts) — backward compatible.

## Capabilities
### Modified Capabilities
- `admin-api`: the admin endpoints can be isolated to a dedicated host.

## Impact
- **Code**: `AdminApiOptions` (add `Host`), `AdminEndpoints.MapAdminApi` (apply `RequireHost`), `Program.cs` (scope
  `/metrics` too). Integration-tested (`DockYarp.IntegrationTests`).
- **Docs (user-facing — new app-config key)**: docs site `configuration.md` (Application configuration) + update the
  `examples.md` caveat now that isolation exists.
- **Scope**: the MVP the user asked for ("à minimum hôte admin dédié") — the **blocking** collision fix. Serving the
  admin host over a **valid ACME certificate** needs a small cross-module cert-desire injection (TLS must not depend
  on AdminApi) → split to the follow-up `add-admin-host-cert`. Until then the admin host uses the fallback cert or an
  operator-provided one.
- **Owning agent**: AG-AA. Resolves the blocking half of `isolate-admin-api`.
