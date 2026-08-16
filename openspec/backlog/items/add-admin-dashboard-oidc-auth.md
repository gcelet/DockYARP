---
id: add-admin-dashboard-oidc-auth
capability: admin-api
agent: AG-AA
tier: B-runtime
priority: low
nginx-proxy: n/a (DockYarp value-add, follow-up to add-admin-dashboard-ui)
provenance: 2026-08-16 user request during add-admin-dashboard-ui scoping — deferred, do not build yet
status: backlog
---

## Why
`add-admin-dashboard-ui` ships v1 relying on network isolation only (`AdminApi:Host` never internet-exposed,
no application-level auth) — the confirmed production posture today. The user wants **OIDC** pluggable later,
so the dashboard can sit behind a real identity provider instead of (or in addition to) network trust, when
that's wanted.

## Current state
- Not started. `add-admin-dashboard-ui` deliberately registers the dashboard's routes through their own
  extension method (not folded into `MapAdminApi`) specifically so an auth requirement can be layered on here
  without restructuring the endpoint mapping — check that seam still holds before designing this item.
- No OIDC/authentication-provider integration exists anywhere in DockYarp today (the Admin API's `/api/*` uses a
  static API key via `ApiKeyEndpointFilter`, not a federated identity flow).

## Proposed change (sketch)
- Add `Microsoft.AspNetCore.Authentication.OpenIdConnect`, config-driven (authority, client id/secret, scopes)
  under a new `AdminApi:Oidc` (or similar) section — **opt-in**, absent config = today's network-isolation-only
  behavior (no breaking change).
- When configured, require authentication on the dashboard's route group; decide whether it should also cover
  `/api/*` (probably not — that's the existing API-key surface, a different consumer: automation/scripts vs. a
  human in a browser) or stay dashboard-only.
- Decide session handling (cookie-based, ASP.NET Core's standard OIDC + cookie auth handler pairing).

## Acceptance criteria (→ scenarios)
- **WHEN** `AdminApi:Oidc` is configured **THEN** the dashboard requires authentication via the configured OIDC
  provider before rendering.
- **WHEN** `AdminApi:Oidc` is not configured **THEN** behavior is unchanged from `add-admin-dashboard-ui` (network
  isolation only) — no regression for operators who don't want this.

## Notes / risks / references
- Genuinely deferred — do not start until `add-admin-dashboard-ui` has shipped and the user actually wants this.
- Refs: `add-admin-dashboard-ui` (prerequisite), `src/DockYarp.AdminApi/ApiKeyEndpointFilter.cs` (the existing,
  separate auth mechanism this does NOT replace).
