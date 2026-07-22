## 1. YARP mapping (AG-RP)

- [x] 1.1 `YarpConfigMapper.BuildRoute` sets `RouteConfig.Transforms` to `{ PathRemovePrefix: prefix }` when the rule carries a transform, else `null`

## 2. Label parsing & mapping (AG-DD)

- [x] 2.1 Add `VirtualDest` constant to `DockerLabels`
- [x] 2.2 Add `PathRemovePrefix` to `ContainerLabelConfig`; parser sets it to `VIRTUAL_PATH` when `VIRTUAL_DEST` and `VIRTUAL_PATH` are both present
- [x] 2.3 `HostGroup.BuildRoute` builds `RouteTransforms` from the parsed prefix

## 3. Tests & docs (AG-RP / AG-DD)

- [x] 3.1 Mapper test: rule with `PathRemovePrefix` → transform entry; no transform → `Transforms` null
- [x] 3.2 End-to-end proxy test: `/api/orders` to a route with `PathRemovePrefix=/api` reaches the backend as `/orders`
- [x] 3.3 Docker mapper test: `VIRTUAL_PATH=/api` + `VIRTUAL_DEST=/` → route strips `/api`; no `VIRTUAL_DEST` → no transform
- [x] 3.4 Document `VIRTUAL_DEST` in `docs/labels-reference.md`
- [x] 3.5 Build + full test suite green via the Nuke CLI
