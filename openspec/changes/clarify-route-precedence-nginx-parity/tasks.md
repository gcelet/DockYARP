## 1. Remove the misguided e2e priority scenario (AG-DEP)

- [x] 1.1 BackendCatalog: remove the `*.priority.local` and `exact.priority.local` backends
- [x] 1.2 Remove `RoutingTests.Priority_HigherWins`

## 2. Clarify the spec (AG-RP)

- [x] 2.1 `proxy-routing` "Host and path matching": reword precedence (host specificity first, then priority/path
      within a host) and add the "Exact host wins over a higher-priority wildcard" scenario

## 3. Documentation (AG-RP)

- [x] 3.1 `docs/architecture.md` parity matrix: host/path selection matches nginx-proxy; note `DOCKYARP_PRIORITY`
      as a DockYarp extension
- [x] 3.2 `docs/labels-reference.md`: mark `DOCKYARP_PRIORITY` as a DockYarp extension (no nginx-proxy equivalent)

## 4. Build & validation

- [x] 4.1 `./build.ps1 Test` green
- [x] 4.2 `openspec validate clarify-route-precedence-nginx-parity --strict`
