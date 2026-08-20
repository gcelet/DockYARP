---
id: add-admin-dashboard-cert-download
capability: admin-api
agent: AG-AA
tier: B-runtime
priority: low
nginx-proxy: n/a (DockYarp value-add)
status: backlog
provenance: 2026-08-20 user feedback, same session as `change-cert-store-format-to-pem` — "on devrait pouvoir
  [...] les récupérer depuis le dashboard admin"
---

## Why

An operator currently has no way to retrieve a certificate DockYarp holds (ACME-issued or operator-provided)
without `docker exec`/volume access to the host. The user wants this exposed from the admin dashboard.

## Current state

- `ICertificateInventory.List()` (`src/DockYarp.AdminApi/ICertificateInventory.cs`) is **deliberately**
  documented as "a sanitized view of stored certificates... no private keys" — the dashboard's certs panel
  (`add-admin-dashboard-ui`) already reads this for the existing host/expiry table. A download feature crosses
  that exact, currently-deliberate boundary: it needs the private key material `ICertificateInventory` was
  explicitly built to exclude.
- The dashboard (`DockYarp.Dashboard`, a Razor Class Library) today reads store data **in-process** — no HTTP
  call to `/api/*`, so the existing admin API key never reaches the browser (per `add-admin-dashboard-ui`'s
  design). A download response containing a private key would be the dashboard's first payload with material
  more sensitive than status/expiry info.
- Auth today is network isolation only (`AdminApi:Host` trust boundary) — no application-level login. The
  deferred `add-admin-dashboard-oidc-auth` stub exists precisely for "when something on the dashboard actually
  needs it" — this feature is a concrete candidate to be that trigger, not a hypothetical one anymore.

## Proposed change (sketch)

Not designed — the central open question, to resolve explicitly at propose/design time, not guessed here:

- **Does downloading (leaf-only vs. leaf+key) need to be gated beyond today's network-isolation-only posture?**
  Consider splitting: downloading the **public certificate** (leaf, no key — e.g. for feeding into a client
  trust store) is no more sensitive than what's already inspectable via `openssl s_client`; downloading the
  **private key** is a materially different risk (credential exfiltration if the dashboard's network boundary
  is ever weaker than assumed). These two may deserve different postures — do not assume they're the same
  decision.
- If gating beyond network isolation is wanted, this is the concrete driver to actually build
  `add-admin-dashboard-oidc-auth` (or a lighter-weight gate — e.g. a confirmation step, or a separate
  `AdminApi:Oidc`-gated route group) rather than leaving it indefinitely deferred.
- Depends on `change-cert-store-format-to-pem` for *what* gets served (a PEM download is a direct file read
  once that ships; against today's PFX-only storage, downloading "as PEM" would need an on-the-fly conversion
  — see `add-admin-dashboard-cert-conversion`, likely the same underlying mechanism).

## Acceptance criteria (→ scenarios)

- **WHEN** an operator requests a certificate download from the dashboard **THEN** they receive the certificate
  material for a host that DockYarp actually holds, in a form immediately usable outside the container
  (no manual reassembly needed).
- **WHEN** the security posture question above is resolved **THEN** the chosen gate (network isolation only,
  OIDC, or something narrower) is actually enforced on the download path — not left as an unenforced
  assumption.

## Notes / risks / references

- Depends on / sequence with `add-admin-dashboard-oidc-auth` and `change-cert-store-format-to-pem` — do not
  propose this in isolation without re-reading both.
- Overlaps in mechanism with `add-admin-dashboard-cert-conversion` (same session's third feedback item) — worth
  assessing at propose time whether they're one feature (download with a format parameter) or two.
