## 1. Store reload (AG-SEC)
- [x] 1.1 `HtpasswdStore`: hold the parsed files in a `volatile` immutable snapshot; add `Reload()` that rebuilds
      and atomically swaps it; `Find` reads the snapshot
- [x] 1.2 `HtpasswdStore.Reload()`: skip a per-file `IOException` (partial write) for the cycle

## 2. Reload service + config (AG-SEC)
- [x] 2.1 `SecurityHeadersOptions`: add `HtpasswdReloadInterval` (default 30s)
- [x] 2.2 `HtpasswdReloadService` (`BackgroundService`): reload the store on a `PeriodicTimer`
- [x] 2.3 Register the service only when `HtpasswdDirectory` is configured

## 3. Tests (AG-SEC)
- [x] 3.1 `HtpasswdStore.Reload()` reflects an edited file (new credentials, old gone)
- [x] 3.2 `HtpasswdStore.Reload()` drops a removed file

## 4. Verify (AG-SEC)
- [x] 4.1 Nuke `Test` gate green
