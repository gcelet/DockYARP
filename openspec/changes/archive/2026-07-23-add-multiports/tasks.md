## 1. Dependency & parsing (AG-DD)

- [x] 1.1 Add `YamlDotNet` to `Directory.Packages.props` and reference it in `DockYarp.Docker`
- [x] 1.2 Add `VirtualHostMultiports` label; add `MultiportEntry` and a pure `MultiportParser.TryParse`
- [x] 1.3 Add `LabelParser.ParseCommon(labels)` for container-level attributes (no host/port)

## 2. Mapping (AG-DD)

- [x] 2.1 `ContainerMapper.Map` branches: multiports containers use a new producer keyed by `(host, path)`; classic path unchanged
- [x] 2.2 Multiports routes/clusters apply container-level attributes (TLS when host ∈ LETSENCRYPT_HOST; dest → prefix strip)

## 3. Tests & docs

- [x] 3.1 Parser tests: YAML → entries (host/path/port/proto/dest); invalid YAML → failure
- [x] 3.2 Mapper tests: multiple ports on one host; multiple hosts; invalid YAML warns; classic still works
- [x] 3.3 Document `VIRTUAL_HOST_MULTIPORTS` in `docs/labels-reference.md`
- [x] 3.4 Build + full test suite green via the Nuke CLI
