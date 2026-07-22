## 1. Model & parsing (AG-DD)

- [x] 1.1 Add `ContainerHealth` enum (`None`, `Starting`, `Healthy`, `Unhealthy`) and `ContainerInfo.Health` (default `None`)
- [x] 1.2 Add `ContainerStatusParser` with `ParseHealth(status)` and `MapAction(action)` (any `health_status*` → `Updated`)

## 2. Source & mapping (AG-DD)

- [x] 2.1 `DockerContainerSource` populates `Health` from the list status and maps events via `MapAction`
- [x] 2.2 `ContainerMapper` skips `Unhealthy`/`Starting` containers (with a warning), keeping healthy siblings

## 3. Tests & docs (AG-DD)

- [x] 3.1 Parser tests: status → health (healthy/unhealthy/starting/none); action → kind (incl. `health_status` → Updated)
- [x] 3.2 Mapper tests: healthy routed; unhealthy excluded + warning; no health check routed; unhealthy sibling dropped, healthy kept
- [x] 3.3 Document health-aware discovery in `docs/docker-discovery.md`
- [x] 3.4 Build + full test suite green via the Nuke CLI
