## 1. Model (AG-RP)

- [x] 1.1 Add `Cluster.RequestTimeout` (`TimeSpan?`) and `RouteRule.MaxRequestBodySize` (`long?`)

## 2. Discovery (AG-DD)

- [x] 2.1 Add `DOCKYARP_PROXY_TIMEOUT` / `DOCKYARP_MAX_BODY_SIZE` labels; parse into `ContainerLabelConfig` with `HasInvalidProxyTimeout`/`HasInvalidMaxBodySize`
- [x] 2.2 `ContainerMapper` sets the cluster timeout and route body-size limit and warns on invalid values

## 3. Proxy application (AG-RP)

- [x] 3.1 `YarpConfigMapper.BuildCluster` maps `Cluster.RequestTimeout` to `ForwarderRequestConfig.ActivityTimeout`
- [x] 3.2 Add `RequestBodySizeMiddleware` (route-aware) that sets the request max body size; register and add before `MapReverseProxy`

## 4. Tests & docs

- [x] 4.1 Parser/mapper tests: labels parsed and carried; invalid → warning
- [x] 4.2 YARP mapper test: cluster timeout → `ActivityTimeout`
- [x] 4.3 Middleware test: route limit applied to the body-size feature; no limit leaves it unchanged
- [x] 4.4 Document the labels in `docs/labels-reference.md`
- [x] 4.5 Build + full test suite green via the Nuke CLI
