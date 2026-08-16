## Context

Verified in code before scoping this change: `src/DockYarp.App/Program.cs:123` calls `app.MapDockYarpAdmin()`
unconditionally, before `app.MapReverseProxy()` (line 125); `AdminEndpointMapping.cs:15-22` always maps the
admin API and Prometheus endpoint with no `ApiKey`/switch-based guard; `AdminEndpoints.cs:27-34` applies
`RequireHost(adminHost)` only `if (host is { Length: > 0 })`. See `proposal.md` - Why for the full analysis.
Three design decisions were confirmed with the user rather than assumed (see below), including a mid-review
pivot from two independent bools to a single three-state enum.

## Goals / Non-Goals

**Goals:**
- A real off switch: when not opted in, `/api/*`, `/metrics`, and `/dashboard` are not mapped at all — not
  mapped-and-401ing.
- The three requested states (off / API-only / API+dashboard) as an **exhaustive, mutually exclusive** set —
  no representable combination should be meaningless.
- `AdminApi:Host` is enforced (not just recommended) whenever the surface is turned on, since it is also the
  dashboard's only trust boundary.

**Non-Goals:**
- Not changing `AdminApi:ApiKey`'s own behavior (still optional; the filter still 401s when unset) — that is a
  separate, narrower concern than "is the surface mapped at all", and conflating the two would widen this change
  beyond what was scoped with the user.
- Not adding a dedicated admin port — noted in the backlog as a related, not-yet-stubbed follow-up; out of scope
  here.
- Not retrofitting the switch to auto-infer from `ApiKey` presence — considered as a backward-compatible
  default and explicitly rejected by the user in favor of the safer, breaking default (see Decisions).

## Decisions

- **A single `AdminApi:Surface` enum (`Disabled`/`Api`/`ApiAndDashboard`), not two independent bools.** The
  first draft of this change used `AdminApi:Enabled` × `AdminApi:DashboardEnabled`. Reviewing it, the user
  pointed out that two independent bools make `Enabled=false, DashboardEnabled=true` representable in config —
  functionally harmless (the code ANDs them, so the dashboard still doesn't serve), but a config surface that
  allows a meaningless combination is exactly the kind of thing that confuses an operator who sets
  `DashboardEnabled=true`, expects `/dashboard` to work, and can't tell why it 404s. Collapsing the two axes
  into one three-state enum makes the illogical combination unrepresentable rather than merely harmless. This
  also matches an existing pattern already in the codebase for small closed state spaces —
  `src/DockYarp.Tls/TlsVersion.cs` and `src/DockYarp.Docker/Discovery/AddressFamilyPreference.cs` were both
  deliberately modeled as enums instead of bool flags (the latter's own commit message cites the `AV1564`
  analyzer rule against non-binary bool params) — so this isn't a new idiom for the project, just applying the
  existing one here. Cost accepted: `AdminApi:DashboardEnabled` (shipped very recently, same session) is
  removed rather than deprecated — pre-1.0, so a clean removal was preferred over carrying a second, overlapping
  toggle.
- **`AdminApi:Surface` defaults to `Disabled` (breaking), not inferred from `ApiKey`.** An inferred default
  (`Api` iff `ApiKey` is set) was proposed as the non-breaking option but rejected: it would leave the exact
  failure mode that prompted this change reachable again the moment an operator sets `ApiKey` without realizing
  `/api/*`/`/metrics` were already being intercepted — the whole point is an explicit, legible switch, not an
  inferred one. The cost is accepted as a documented breaking change (proposal.md marks it **BREAKING**) with
  every doc recipe using the admin API updated in this same change.
- **Non-`Disabled` with `Host` unset fails fast at startup, not a warning.** A logged warning was considered and
  rejected: `AdminApi:Host` is not just about avoiding path-shadowing, it is the dashboard's *only* trust
  boundary (no application-level auth, per the existing "Read-only admin dashboard" requirement). Silently
  allowing an operator to expose the dashboard/API on every host is a real safety gap, not a style nit — a
  fail-fast configuration error is more consistent with how the project already treats hard prerequisites
  elsewhere (e.g. Data Protection's certificate-path gate) than a log line that can go unnoticed.
- **Fail-fast implementation: a plain `throw new InvalidOperationException(...)` guard, not `IValidateOptions`.**
  Checked via `microsoft-docs` what ASP.NET Core's idiomatic mechanism is (`IValidateOptions<TOptions>` +
  `ValidateOnStart()`) — then checked the codebase itself and found DockYARP doesn't use the `IOptions<T>`
  pattern anywhere; every options class (`AdminApiOptions` included, in
  `ObservabilityServiceCollectionExtensions.AddDockYarpObservability`) is a plain POCO bound once via
  `configuration.GetSection(...).Bind(instance)` and registered as a singleton instance. The project's own
  existing fail-fast precedent (`DataProtectionSetup.LoadEncryptionCertificate`, throwing
  `InvalidOperationException` with the exact config key and remediation text) already achieves "refuse to start
  on bad config" without the `IOptions` machinery — this change follows that established, already-consistent
  pattern rather than introducing a second validation mechanism for one options class.

## Risks / Trade-offs

- [Risk] Breaking change silently bites an existing deployment that upgrades without reading the changelog (a
  live proxy loses its admin API and dashboard on next restart with no route change, until `Surface` is set) →
  [Mitigation] the fail-fast host check turns "half-migrated" configs into a loud startup error rather than a
  silent gap, and the change's docs sweep updates every recipe in this same commit so a reader following current
  docs never hits it.
- [Risk] The startup-time failure mode (fail-fast on `Surface != Disabled` + empty `Host`) is easy to only
  cover with the happy path in tests → [Mitigation] tasks.md includes an explicit test for the fail-fast case.
- [Risk] Removing `AdminApi:DashboardEnabled` outright (vs. deprecating it) breaks anyone who set only that key
  → [Mitigation] accepted given how recently it shipped (same session) and the project's pre-1.0 status; called
  out explicitly in the docs sweep rather than silently dropped.
