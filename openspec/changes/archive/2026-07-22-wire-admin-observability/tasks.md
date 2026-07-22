## 1. Discovery health signal (AG-DD)

- [x] 1.1 Add `DiscoveryHealthState` (thread-safe connected flag) to `DockYarp.Docker`; register it in `AddDockerDiscovery`
- [x] 1.2 `DockerDiscoveryService` sets connected `true` on successful connect/reconcile and `false` on failure/disconnect

## 2. AdminApi abstractions & endpoints (AG-AA)

- [x] 2.1 Add `ICertificateInventory` (returns sanitized `CertView`) and `IDiscoveryHealth` (`Enabled`, `Connected`) to `DockYarp.AdminApi`
- [x] 2.2 Extend `AdminApiModels.HealthView` with certificate count and discovery status
- [x] 2.3 `/api/certs` returns `ICertificateInventory.List()`; `/api/health` computes status from counts + discovery health

## 3. App adapters & wiring (AG-AA)

- [x] 3.1 `CertificateInventoryAdapter` maps `ICertificateStore.List()` → `CertView`
- [x] 3.2 `DiscoveryHealthAdapter` (over `DiscoveryHealthState`) and a disabled variant; register the right one in `Program`
- [x] 3.3 Register `ICertificateInventory` in `Program`

## 4. Tests & docs (AG-AA)

- [x] 4.1 Integration: `/api/certs` returns a seeded certificate (via an overridden inventory); `/api/health` is `Healthy` (discovery disabled) and `Degraded` when discovery is enabled+disconnected
- [x] 4.2 Update `docs/admin-api.md`
- [x] 4.3 Build + full test suite green via the Nuke CLI
