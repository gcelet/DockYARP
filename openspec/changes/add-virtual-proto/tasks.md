## 1. Routing model (AG-RP)

- [x] 1.1 Add `BackendScheme` enum (`Http`, `Https`) to `DockYarp.Core.Models`
- [x] 1.2 Add a `ClusterEndpoint.Create(id, scheme, host, port)` factory that composes the absolute address

## 2. Label parsing & mapping (AG-DD)

- [x] 2.1 Add `VirtualProto` constant to `DockerLabels`
- [x] 2.2 Add `Scheme` (`BackendScheme`, default `Http`) to `ContainerLabelConfig`; parse `VIRTUAL_PROTO` in `LabelParser`
- [x] 2.3 Add `LabelParser.HasUnsupportedProto(labels)`; `ContainerMapper` builds endpoints via the scheme and warns on unsupported values

## 3. Tests & docs (AG-RP / AG-DD)

- [x] 3.1 Core test: `ClusterEndpoint.Create` with `Https` → `https://…`; default → `http://…`
- [x] 3.2 Mapper tests: `VIRTUAL_PROTO=https` → HTTPS endpoint; unsupported value → HTTP endpoint + warning
- [x] 3.3 Document `VIRTUAL_PROTO` in `docs/labels-reference.md`
- [x] 3.4 Build + full test suite green via the Nuke CLI
