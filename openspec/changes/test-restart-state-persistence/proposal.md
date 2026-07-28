## Why
`persist-state-on-writable-volume` made DockYarp persist certificates and Data Protection keys to a
non-root-writable volume, and its deployment spec declares a scenario "State survives container recreation".
The e2e proved the **write** path (certs provision without a permission error) but never proved **survival across
a restart**: nothing recreates the DockYarp container within a run, so that scenario is asserted only by prose.

This adds one end-to-end scenario that restarts the DockYarp container against the same `/certs` volume and
asserts the previously provisioned ACME certificate is **reused** (same certificate served), proving the
persistent volume carries state across a container recreation — the same volume that also carries the Data
Protection key ring.

## What Changes
- **e2e AppHost**: set `Tls__RenewBeforeExpiry` below step-ca's certificate lifetime so a provisioned certificate
  is **not renewed** during a run. Without this the proxy renews every `CheckInterval` (5s) — step-ca issues
  ~24h certificates while the default renewal margin is 30 days — which churns the certificate thumbprint and
  would make any reuse assertion flaky. This also makes the whole TLS suite more deterministic.
- **e2e fixture**: expose a helper that restarts the `dockyarp` resource (`ResourceCommandService` +
  `KnownResourceCommands.RestartCommand`) and waits for it to become healthy again, leaving the shared proxy
  usable for the rest of the (sequential) suite.
- **e2e test**: a new `RestartPersistenceTests` scenario — provision a certificate for a TLS host, capture its
  thumbprint, restart the container, and assert the served certificate is the same thumbprint (reused from the
  persisted volume, not re-provisioned).
- **e2e harness robustness**: make the cert-directory reset in `TlsHarness` best-effort. The non-root container
  creates a `dataprotection-keys/` subdirectory owned by the app uid, which the host process cannot delete, so a
  second run failed at setup with a permission error. The reset now swallows those permission errors and reuses
  the harmless leftover state (surfaced by this change's first repeat e2e run; a latent defect from
  `persist-state-on-writable-volume`, which only bites on the second run).

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `deployment`: the end-to-end suite additionally asserts that persisted state (a provisioned certificate)
  survives a container restart against the same volume.

## Impact
- **Tests/infra**: `tests/DockYarp.E2E.AppHost/Program.cs` (renewal margin), `tests/DockYarp.E2E.Tests`
  (fixture restart helper + new `RestartPersistenceTests` + best-effort cert-directory reset in `TlsHarness`).
  No product code changes.
- **Deferred (more coherent, backlog `e2e-stepca-long-cert-duration`)**: instead of shortening the e2e renewal
  margin, have step-ca issue certificates longer than the default 30-day renewal margin so DockYarp keeps its
  realistic default. Deferred because step-ca exposes no duration env — it requires patching `ca.json`'s
  `authority.claims` before serve (no hot-reload), i.e. restructuring the working step-ca setup, which cannot be
  validated locally (Docker runs only in the e2e environment).
- **Owning agent**: AG-DEP.
- **Runtime**: validated by the next `E2E` run — the restart scenario passes and `dockyarp.log` shows a second
  startup (the restart) with neither `FileSystemXmlRepository[60]` nor `XmlKeyManager[35]`.
- **Backlog**: resolves `test-restart-state-persistence`.
