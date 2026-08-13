## 1. Host-scoping (AG-AA)
- [x] 1.1 `AdminApiOptions`: add `Host` (`string?`, default null) with XML doc
- [x] 1.2 `AdminEndpoints.MapAdminApi(this IEndpointRouteBuilder, string? host)`: `RequireHost(host)` on the `/api` group when set (required param — AV1553 forbids an optional null default)
- [x] 1.3 `Program.cs`: resolve `AdminApiOptions`, pass `Host` to `MapAdminApi`, and `RequireHost` the `/metrics` endpoint too (extracted to `AdminEndpointMapping.MapDockYarpAdmin` — AV1500)

## 2. Tests (AG-AA)
- [x] 2.1 `AdminApiIntegrationTests`: with `AdminApi:Host=admin.local`, `/api/health` on `other.local` is not the admin 401 (falls through); on `admin.local` it is the admin 401
- [x] 2.2 Existing admin tests (no `AdminApi:Host`) still pass (all-hosts default unchanged)

## 3. Docs (AG-DOC — user-facing config key)
- [x] 3.1 docs site `configuration.md`: document `AdminApi:Host` (Application configuration reference)
- [x] 3.2 `examples.md`: update the caveat now that a dedicated admin host isolates `/api`

## 4. Verify (AG-AA)
- [x] 4.1 Nuke `Test` gate green (unit + integration), warnings-as-errors clean
