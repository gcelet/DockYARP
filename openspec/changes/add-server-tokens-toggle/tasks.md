## 1. Recognize the key (AG-DD)
- [x] 1.1 `DockerLabels.ServerTokens = "SERVER_TOKENS"`
- [x] 1.2 `ContainerLabelConfig.ServerTokens` (string?)
- [x] 1.3 `LabelParser.TryParse` + `ParseCommon`: `ServerTokens = GetOrNull(labels, DockerLabels.ServerTokens)`

## 2. Carry it per route (AG-DD)
- [x] 2.1 `RouteRule.ServerTokens` (string?, top-level)
- [x] 2.2 `ContainerMapper`: set `ServerTokens` on the `RouteRule` in both classic + multiports builders

## 3. Apply it (AG-SEC)
- [x] 3.1 `SecurityHeadersMiddleware`: resolve the route once; suppress the `Server` header when the per-host
      `SERVER_TOKENS` is `off` (or empty), otherwise emit the global `Security:ServerHeader`

## 4. Tests (AG-SEC / AG-DD)
- [x] 4.1 `LabelParser`: `SERVER_TOKENS` parsed into the config
- [x] 4.2 `SecurityHeadersMiddleware`: per-host `off` suppresses the header while another host keeps the global
      value; `ContainerMapper` carries it top-level (no cert required)

## 5. Verify (AG-SEC)
- [x] 5.1 Nuke `Test` gate green (unit/integration, no Docker) — 311 tests, 0 failures
