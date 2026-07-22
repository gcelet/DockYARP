## 1. Label parsing (AG-DD)

- [x] 1.1 Add `Priority` constant to `DockerLabels` and `Priority` (int) to `ContainerLabelConfig`
- [x] 1.2 `LabelParser` parses `DOCKYARP_PRIORITY` (invariant int, default `0`); add `HasInvalidPriority(labels)`

## 2. Mapping (AG-DD / AG-RP)

- [x] 2.1 `ContainerMapper` sets `RouteRule.Priority` and warns on an invalid value
- [x] 2.2 `YarpConfigMapper.BuildRoute` maps priority to `Order` (`0` → unset, else `-priority`)

## 3. Tests & docs (AG-DD / AG-RP)

- [x] 3.1 Parser tests: numeric priority parsed; absent → 0; non-numeric → 0 + `HasInvalidPriority`
- [x] 3.2 Mapper tests: priority set on the route; invalid → warning; YARP order = `-priority` and unset for 0
- [x] 3.3 Document `DOCKYARP_PRIORITY` in `docs/labels-reference.md`
- [x] 3.4 Build + full test suite green via the Nuke CLI
