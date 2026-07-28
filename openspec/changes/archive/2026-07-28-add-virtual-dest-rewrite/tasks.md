## 1. Model (AG-RP)
- [x] 1.1 `RouteTransforms`: add `PathAddPrefix` (destination prefix prepended after stripping)
- [x] 1.2 `ContainerLabelConfig`: add `PathAddPrefix`

## 2. Parsing + mapping (AG-RP)
- [x] 2.1 `DockYarp.Docker`: add a shared `PathRewrite.Resolve(dest, path)` → (Remove, Add)
- [x] 2.2 `LabelParser`: set `PathRemovePrefix` + `PathAddPrefix` from `VIRTUAL_DEST`/`VIRTUAL_PATH` via the resolver
- [x] 2.3 `ContainerMapper`: thread `PathAddPrefix` into `RouteTransforms` on the classic and multiports paths
- [x] 2.4 `YarpConfigMapper`: emit `PathRemovePrefix` then `PathPrefix` (in order) when present

## 3. Split regex VIRTUAL_PATH (AG-RP)
- [x] 3.1 New backlog item `add-regex-virtual-path` (regex locations + regex-path ↔ `VIRTUAL_DEST` incompatibility)

## 4. Tests (AG-RP)
- [x] 4.1 `LabelParser`/`PathRewrite`: dest `/v2` → remove `/api` + add `/v2`; dest `/` → remove only; dest absent → none
- [x] 4.2 Mapper: a route with remove+add emits `PathRemovePrefix` then `PathPrefix`; multiports carries the dest

## 5. Verify (AG-RP)
- [x] 5.1 Nuke `Test` gate green
