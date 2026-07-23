## 1. Fix (AG-RP / AG-DD)

- [x] 1.1 `YarpConfigMapper.BuildCluster` builds destinations with a last-wins dictionary (de-dupe by endpoint id)
- [x] 1.2 `LabelParser.SplitHosts` de-duplicates hosts (case-insensitive)

## 2. Tests & build

- [x] 2.1 Mapper test: a cluster with two same-id endpoints maps to one destination without throwing
- [x] 2.2 Parser test: `VIRTUAL_HOST=app.local,app.local` yields a single host
- [x] 2.3 Build + full test suite green via the Nuke CLI
