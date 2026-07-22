## 1. Label parsing (AG-DD)

- [x] 1.1 Change `ContainerLabelConfig.Host` (string) to `Hosts` (`ImmutableArray<string>`)
- [x] 1.2 In `LabelParser`, split `VIRTUAL_HOST` on commas (trim, drop empties) via a `SplitHosts` helper
- [x] 1.3 Fail with the existing "required" error when no valid host remains

## 2. Mapping (AG-DD)

- [x] 2.1 `ContainerMapper.GroupByHost` fans the container out to one `HostGroup` per host in `config.Hosts`

## 3. Tests & docs (AG-DD)

- [x] 3.1 Parser tests: single host → one entry; comma-separated → multiple; whitespace/empty tolerated
- [x] 3.2 Mapper test: `a.local,b.local` produces routes/clusters for both hosts targeting the container
- [x] 3.3 Document comma-separated `VIRTUAL_HOST` in `docs/labels-reference.md`
- [x] 3.4 Build + full test suite green via the Nuke CLI
