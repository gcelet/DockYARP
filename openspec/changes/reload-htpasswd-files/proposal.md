## Why
`add-htpasswd-files` loads htpasswd files once at startup, so adding a user, rotating a password, or removing a
file requires restarting DockYarp. nginx-proxy picks up htpasswd changes without a restart; operators expect the
same.

## What Changes
- `HtpasswdStore` gains a `Reload()` that re-reads the directory and atomically swaps an immutable snapshot, so
  the request path keeps reading lock-free. Per-file read errors (a file mid-write) are skipped for that cycle.
- A `HtpasswdReloadService` (`BackgroundService` on a `PeriodicTimer`) reloads the store on a configurable
  interval (`Security:HtpasswdReloadInterval`, default 30s). It is registered only when an htpasswd directory is
  configured.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `security`: htpasswd credentials are reloaded without a restart when the files change.

## Impact
- **Code**: `DockYarp.Security` (`HtpasswdStore` reload + snapshot swap, new `HtpasswdReloadService`,
  `SecurityHeadersOptions.HtpasswdReloadInterval`, conditional hosted-service registration).
- **Tests**: `DockYarp.Security.Tests` — `HtpasswdStore.Reload()` reflects an edited file and a removed file.
- **Rationale**: a periodic poll (not `FileSystemWatcher`) is robust across container bind mounts, and mirrors
  the existing certificate-provisioning `BackgroundService` pattern.
- **Owning agent**: AG-SEC. Resolves `reload-htpasswd-files`.
