## 1. Recognize the key (AG-DD)
- [x] 1.1 `DockerLabels.MaxConnections = "DOCKYARP_MAX_CONNECTIONS"`
- [x] 1.2 `ContainerLabelConfig.MaxConnectionsPerServer` (int?)
- [x] 1.3 `LabelParser`: `ParsePositiveInt` (int > 0 → int?, else null) wired into `TryParse` + `ParseCommon`;
      `HasInvalidMaxConnections` diagnostic

## 2. Carry it per cluster (AG-DD / AG-RP)
- [x] 2.1 `Cluster.MaxConnectionsPerServer` (int?)
- [x] 2.2 `ContainerMapper`: set `MaxConnectionsPerServer` in both cluster blocks (classic + multiports);
      `AddCommonWarnings` reports `HasInvalidMaxConnections`

## 3. Map it (AG-RP)
- [x] 3.1 `YarpConfigMapper.BuildCluster`: `HttpClient = BuildHttpClientConfig(cluster)` → an `HttpClientConfig`
      with `MaxConnectionsPerServer` when set, else `null`

## 4. Tests (AG-RP / AG-DD)
- [x] 4.1 `LabelParser`: `DOCKYARP_MAX_CONNECTIONS` parsed; invalid value → null + `HasInvalidMaxConnections`
- [x] 4.2 `ContainerMapper`: a cluster carries `MaxConnectionsPerServer`
- [x] 4.3 `YarpConfigMapper`: maps to `HttpClientConfig.MaxConnectionsPerServer`; unset → `HttpClient` is null

## 5. Docs (AG-DOC)
- [x] 5.1 Site configuration reference + `docs/labels-reference.md`: document `DOCKYARP_MAX_CONNECTIONS`

## 6. Verify (AG-RP)
- [x] 6.1 Nuke `Test` gate green (unit/integration, no Docker)
