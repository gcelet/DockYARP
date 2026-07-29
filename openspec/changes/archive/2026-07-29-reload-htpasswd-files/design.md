# Design — reload-htpasswd-files

## Reload strategy: periodic poll
A `BackgroundService` (`HtpasswdReloadService`) reloads the store on a `PeriodicTimer`, mirroring
`CertificateProvisioningService`. A poll is preferred over `FileSystemWatcher` because inotify events are
unreliable across container bind mounts (the common htpasswd deployment), and a poll has no event-storm/debounce
concerns. The interval is `Security:HtpasswdReloadInterval` (default 30s); the service is registered only when
`Security:HtpasswdDirectory` is set, so there is no idle timer otherwise.

## Lock-free reads via snapshot swap
`HtpasswdStore` holds the parsed files in a single `volatile` reference to an immutable dictionary. `Reload()`
builds a brand-new dictionary from the directory and assigns it in one atomic reference write; `Find` takes one
volatile read and operates on that consistent snapshot. Readers on the request path never lock and never observe
a half-updated map.

## Partial writes
A file being written when the poll runs can throw `IOException` (sharing violation) or yield truncated content.
`Reload()` catches per-file `IOException` and skips that file for the cycle (it is picked up on the next poll);
a truncated line simply fails the `user:hash` split and is ignored. A failed reload never throws out of the
timer loop.

## Testing
`HtpasswdStore.Reload()` is public and unit-tested directly (temp directory): editing a file's credentials and
removing a file are both reflected after `Reload()`. The `BackgroundService` itself is a thin timer→`Reload()`
loop and is not timer-tested (that would be flaky); the store's reload logic is the substance.
