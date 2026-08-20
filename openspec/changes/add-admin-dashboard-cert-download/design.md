## Context

See `proposal.md` — Why, and the resolved security-posture question (asked explicitly, not assumed): the user
chose to keep today's network-isolation-only trust boundary (`AdminApi:Host`) as sufficient for private-key
download, on the condition that the feature is **opt-in, disabled by default**, and carries a clear
documentation warning.

Current architecture, confirmed by reading the code, not assumed:
- `ICertificateInventory` (`src/DockYarp.AdminApi/ICertificateInventory.cs`) is explicitly documented "a
  sanitized view of stored certificates... no private keys" — `List()` returns host+expiry only.
- The dashboard (`DockYarp.Dashboard`, a Razor Class Library) reads **in-process** — `IndexModel` takes
  `IRouteConfigStore`/`ICertificateInventory`/`IDiscoveryHealth` as constructor dependencies, never makes an
  HTTP call to `/api/*`. This is deliberate (`add-admin-dashboard-ui`'s own design): the admin API key never
  reaches the browser, since the dashboard never needs to send it anywhere.
- `/api/*` (`AdminEndpoints.MapAdminApi`) is behind `ApiKeyEndpointFilter` — every route in that group requires
  the `X-Api-Key` header. A browser-initiated download link cannot supply a custom header without JavaScript
  fetch + blob-download plumbing, which would also mean embedding the API key in a page the browser can read —
  breaking the exact invariant `add-admin-dashboard-ui` was built to preserve.
- `DashboardEndpointMapping.MapDockYarpDashboard()` already has the right shape for a second gated capability:
  it's a separate extension method (not folded into `MapAdminApi`) specifically so more can be layered onto the
  dashboard's own mapping without touching the JSON API's.
- `CertificateInventoryAdapter` (`src/DockYarp.App/Observability/CertificateInventoryAdapter.cs`) is the
  existing precedent for exposing `ICertificateStore` (in `DockYarp.Tls`, which `DockYarp.AdminApi` cannot
  reference per the module dependency graph — `Core` is the leaf, `AdminApi -> Core`, `App -> everything`) to
  the admin surface via a small interface + App-level adapter.
- `FileCertificateStore`'s `ExportChainPem`/`ExportPrivateKeyPem` (added in `change-cert-store-format-to-pem`)
  are `private static` — the exact PEM-assembly logic this new adapter needs, currently not reusable outside
  that one class.

## Goals / Non-Goals

**Goals:**
- Download the public certificate and the private key for a stored host, from the dashboard, gated by an
  explicit opt-in setting defaulting to off.
- Never route the download through the API-key-protected `/api/*` surface — preserve the existing "no admin API
  key reaches the browser" guarantee exactly as it holds today.
- No duplicated PEM-assembly logic between the certificate store's own write path and this new read/export path.

**Non-Goals:**
- Any new authentication layer (OIDC or otherwise) — explicitly rejected by the user for this change; network
  isolation (`AdminApi:Host`) is the accepted trust boundary, same as everything else the dashboard already
  exposes.
- Format conversion (PEM↔PFX) — that's `add-admin-dashboard-cert-conversion`, a separate backlog item, deferred
  until this ships (its own stub already expects it may fold into this download mechanism via a format
  parameter later; not built now).
- A confirmation step / rate limit / audit log specifically for the private-key download — not requested by the
  user; the opt-in default-off setting plus the docs warning are the agreed mitigation, not an in-product gate.

## Decisions

**Two separate GET routes under the dashboard's own mapping (`{host}/certificate`, `{host}/private-key`), not
a route under `/api/*`.**

Rationale: keeps the browser-initiated download working with a plain `<a href>` link — no fetch/blob/JS needed,
and critically, no admin API key has to reach the browser to make it work. Considered and rejected: exposing
this under `/api/certs/{host}/export` — would require either weakening `ApiKeyEndpointFilter` for this one
route (inconsistent, confusing) or teaching the dashboard's Razor page to fetch with the API key client-side
(reintroduces exactly the exposure `add-admin-dashboard-ui` was designed to avoid).

**New `ICertificateExporter` interface, kept separate from `ICertificateInventory` rather than adding a method
to it.**

Rationale: `ICertificateInventory`'s own doc comment is an explicit, load-bearing contract — "no private keys".
Adding an export method to the same interface would make that comment inaccurate for part of the type's own
surface, and any future code holding an `ICertificateInventory` reference could no longer assume it's safe to
expose the whole interface to a lower-trust context. A second, separate interface keeps that safety property
visible at the type level, not just in a doc comment on one method.

**Extract `FileCertificateStore`'s PEM-building helpers into public extension methods on `LoadedCertificate`
(new file in `DockYarp.Tls`), used by both `Save()` and the new `CertificateExporterAdapter`.**

Rationale: this change is the second real call site for "turn a `LoadedCertificate` into PEM text" — the
`change-cert-store-format-to-pem` design explicitly rejected extracting a shared helper for a *single* call
site as premature; that reasoning no longer applies once a second one exists. `LoadedCertificate` is already a
public type in `DockYarp.Tls`; `DockYarp.App` already references `DockYarp.Tls` directly (it's the module that
wires `ICertificateStore` itself), so a public extension method needs no `InternalsVisibleTo` plumbing.

**`AdminApiOptions.AllowCertificateDownload`, a plain opt-in bool (default `false`), mirroring the existing
`LetsEncrypt` bool on the same options type — not a new enum, not folded into `Surface`.**

Rationale: `Surface`'s three states are about *what admin surface exists at all* (API/dashboard/neither);
whether a specific dashboard feature is available once the dashboard *is* on is an orthogonal, narrower
question — the same shape `LetsEncrypt` already uses for "given the admin host exists, should it also be
ACME-provisioned." Considered and rejected: making download available whenever `Surface == ApiAndDashboard`
with no separate toggle — rejected per the user's explicit request for an independent opt-in default-off
setting, not tied to the dashboard being on at all.

## Risks / Trade-offs

- [Risk] A private key becomes downloadable over HTTP for the first time in this project, protected only by
  network isolation (no application-level auth). → Accepted per the user's explicit decision; mitigated by
  opt-in-default-off and a documentation warning (task in `tasks.md`), not by an additional in-product gate.
- [Risk] An operator enables `AllowCertificateDownload` without reading the warning and exposes it more broadly
  than intended (e.g. `AdminApi:Host` reachable from an untrusted network segment). → Mitigation: the docs
  warning states the risk explicitly and points at `AdminApi:Host`'s own isolation requirement; this is a
  documentation-level mitigation, matching the scope the user asked for (not a code-level enforcement — Non-Goal).
- [Risk] `ICertificateExporter`'s existence as a second, more sensitive interface could be misused later by code
  that doesn't need private-key access if it's registered as broadly available as `ICertificateInventory`. →
  Mitigation: DI-register it the same way (singleton, constructor injection) but only the dashboard's endpoint
  mapping and its own adapter reference it — no other consumer is introduced by this change; a future reviewer
  adding a new `ICertificateExporter` consumer should treat that as a deliberate, reviewable decision, not
  something this change makes easy to do by accident (it's just a normal DI-registered interface, same
  ergonomics as any other).
