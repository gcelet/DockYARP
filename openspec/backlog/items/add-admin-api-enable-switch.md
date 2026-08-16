---
id: add-admin-api-enable-switch
capability: admin-api
agent: AG-AA
tier: B-runtime
priority: high
status: backlog
nginx-proxy: (internal — DockYARP-specific, no parity row)
provenance: 2026-08-16 user's own migration prep — verified against code before writing this stub
---

## Why
`AdminApi:ApiKey` unset is documented as "the admin API is closed" (`AdminApiOptions.cs:6`), which reads as "not
active" — but it is not. **The `/api/*` and `/metrics` routes are always mapped**, regardless of `ApiKey`;
"closed" only means the endpoint filter unconditionally returns 401 instead of ever validating a key
(`ApiKeyEndpointFilter.cs`). Because ASP.NET Core prioritizes an explicit-path endpoint match over YARP's
catch-all proxy route, **any backend proxied through DockYARP that itself exposes a real route at `/api/*` or
`/metrics` gets silently shadowed** — the request is captured by DockYARP's admin routes and 401'd, never
reaching the backend — on **every host**, unless the operator has separately set `AdminApi:Host` (which scopes
the interception via `RequireHost`, but is itself unset by default). This is a correctness/security-adjacent
default-behavior gap: a fresh DockYARP deployment with no admin configuration at all can already break a
backend's own `/api/*` paths, which is surprising and has no valid reason to happen when the operator never
opted into the admin surface.

Verified in code before writing this stub (not assumed):
- `src/DockYarp.App/Program.cs:123` — `app.MapDockYarpAdmin()` runs unconditionally, before
  `app.MapReverseProxy()` (line 125).
- `src/DockYarp.App/Observability/AdminEndpointMapping.cs:15-22` — always maps the admin API + Prometheus
  scraping endpoint; no `ApiKey`-based guard anywhere.
- `src/DockYarp.AdminApi/AdminEndpoints.cs:27-34` — `RequireHost(adminHost)` only applies
  `if (host is { Length: > 0 })`; when `AdminApi:Host` is unset, no host constraint exists at all.
- `tests/DockYarp.IntegrationTests/AdminApiIntegrationTests.cs:164-187` only covers the Host-**set** case
  (`AdminHost_OtherHostIsNotShadowed`); no test exercises Host-unset against a backend owning `/api/*` or
  `/metrics`, and no test asserts "ApiKey unset ⇒ routes not mapped."

## nginx-proxy behavior
N/A — DockYARP-specific admin surface, no nginx-proxy equivalent. No `parity.md` row.

## DockYarp today
- `AdminApiOptions` (`src/DockYarp.AdminApi/AdminApiOptions.cs`) has `ApiKey`, `Host`, `LetsEncrypt`,
  `ContactEmail` — no explicit `Enabled`-style switch. `ApiKey` presence is used as an implicit "is the admin
  API meaningfully usable" signal, but the routes are mapped either way.
- `AdminApi:DashboardEnabled` (default `true`, shipped with `add-admin-dashboard-ui`) already correctly gates
  the Razor Pages dashboard — both its DI registration and route mapping are skipped when `false`. **This is
  the pattern to mirror for the API/metrics routes themselves**, which have no equivalent today.
- User's requested shape (verbatim intent): a clear switch with (at least) two meaningfully different enabled
  states — API endpoints only, vs. API endpoints **and** dashboard — plus a real "fully off" state where nothing
  is mapped, not just 401'd.

## Proposed change (sketch)
- New explicit enable switch (naming TBD at propose time — e.g. `AdminApi:Enabled`, default `false`) that gates
  whether `MapAdminApi`/`MapPrometheusScrapingEndpoint` are called **at all** — mirroring how
  `AdminApi:DashboardEnabled` already gates the dashboard. When `false`, `/api/*` and `/metrics` fall through to
  YARP like any other path — no shadowing, regardless of what a backend does with those paths.
  `DashboardEnabled` should probably become dependent on (or at least documented against) the new switch, since
  a dashboard with no API underneath it doesn't make sense.
  - Open question for propose/design: default value. `false` is the safe-by-default choice (matches the actual
    complaint), but is a breaking behavior change for anyone currently relying on the current always-on
    behavior without realizing it — needs a decision + a changelog/migration note either way.
  - Open question: should enabling without `Host` set be blocked/warned (forcing the safe, scoped usage) rather
    than silently allowing all-hosts interception? Related to the not-yet-stubbed "dedicated admin PORT"
    follow-up already noted in the backlog-queue memory — worth reading together at propose time, not deciding
    here.
- Update `configuration.md`'s AdminApi table + `examples.md`'s existing reserved-path warning to describe the
  real (verified) behavior, not the "closed means closed" reading the current wording implies.
- Add the missing test coverage identified above: Host-unset + a backend on `/api/*`/`/metrics` (shadowing,
  today's actual behavior, to lock in until fixed); then the fixed behavior once the switch exists.

## Acceptance criteria (→ scenarios)
- **WHEN** the new switch is off (default) **THEN** `/api/*` and `/metrics` are not mapped at all and a
  backend's own routes at those paths are proxied normally on every host.
- **WHEN** the switch is on for API-only **THEN** the endpoints behave as today (subject to `Host`/`ApiKey`),
  and the dashboard is not registered.
- **WHEN** the switch is on for API+dashboard **THEN** both are available, same as `DashboardEnabled: true`
  today.
- **WHEN** a reader consults `configuration.md`/`examples.md` **THEN** the shadowing risk and how to avoid it
  (switch off, or `Host` scoping) is stated accurately.

## Notes / risks / references
- Discovered while reviewing the real migration compose (the user asked why `AdminApi:Host` mattered;
  verifying the answer surfaced this broader default-behavior gap).
- Distinct from, but related to, the not-yet-stubbed **dedicated admin PORT** follow-up already noted in
  `backlog-work-queue` memory (exposing admin on its own port breaks HTTP-01 ACME) — read both together before
  designing, they likely share the `Host`/exposure discussion.
- Refs: `src/DockYarp.AdminApi/AdminApiOptions.cs`, `AdminEndpoints.cs`, `ApiKeyEndpointFilter.cs`,
  `src/DockYarp.App/Observability/AdminEndpointMapping.cs`, `src/DockYarp.App/Program.cs:118-125`,
  `tests/DockYarp.IntegrationTests/AdminApiIntegrationTests.cs`.
