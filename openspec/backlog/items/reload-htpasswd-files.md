---
id: reload-htpasswd-files
capability: security
agent: AG-SEC
tier: B-runtime
priority: low
status: backlog
nginx-proxy: (internal finding — nginx reloads htpasswd on change)
provenance: deferred from add-htpasswd-files, 2026-07-29
---

## Why
`add-htpasswd-files` loads htpasswd files **once at startup** (`HtpasswdStore` reads the directory in its
constructor). Changing an htpasswd file (adding/removing a user, rotating a password) currently requires
restarting DockYarp. nginx-proxy picks up htpasswd changes without a restart, so operators expect the same.

## nginx-proxy behavior
- htpasswd files under the mounted directory are consulted per request; editing a file takes effect without a
  reload/restart.

## DockYarp today
- `src/DockYarp.Security/HtpasswdStore.cs` reads all files under `Security:HtpasswdDirectory` in its constructor
  (a singleton), so the credential set is fixed for the process lifetime.

## Proposed change (sketch)
Reload the htpasswd store when files change. Options:
- a `FileSystemWatcher` on the directory (debounced), or
- a periodic re-read (a hosted service on a timer, like certificate provisioning), or
- re-read on each request with a short cache + file-mtime check.
Keep it allocation-light and never log credentials; handle partial writes (a file mid-edit) gracefully.

## Acceptance criteria (→ scenarios)
- **WHEN** an htpasswd file is added or edited while DockYarp is running
- **THEN** the new credentials take effect without a restart (within a bounded delay)
- **WHEN** an htpasswd file is removed
- **THEN** its protection is dropped without a restart

## Notes / risks / references
- Internal finding — no `parity.md` row of its own (referenced from the htpasswd row).
- Watch for FileSystemWatcher cross-platform quirks (container bind mounts); a debounced poll may be more robust.
- Sibling (done): `add-htpasswd-files` (startup load).
