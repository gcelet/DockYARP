## 1. Core (AG-RP)

- [x] 1.1 Add `IStaticConfigProvider` (returns a `Static` `ConfigContribution`) and `EmptyStaticConfigProvider`

## 2. App provider & applier (AG-RP)

- [x] 2.1 Add `StaticConfigOptions` (`Path`) and a JSON DTO
- [x] 2.2 Add `StaticConfigProvider` (reads via `IFileSystem`, maps to routes/clusters, fail-open + source-gen logs)
- [x] 2.3 Add `StaticConfigService` (hosted) applying the static contribution at startup

## 3. Discovery & wiring (AG-RP / AG-DD)

- [x] 3.1 `DiscoveryReconciler` merges `[static, dynamic]`
- [x] 3.2 `Program` registers the provider always; registers `StaticConfigService` only when Docker is disabled

## 4. Tests & docs

- [x] 4.1 Provider tests (MockFileSystem): JSON → routes/clusters; missing/invalid → empty
- [x] 4.2 Reconciler test: static contribution wins over discovery; applier test: static applied to the store
- [x] 4.3 Document static config in `docs/routing-model.md` (+ file format)
- [x] 4.4 Build + full test suite green via the Nuke CLI
