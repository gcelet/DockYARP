## 1. Recognize the namespaced keys (AG-DD)
- [x] 1.1 `DockerLabels.NginxLoadBalance = "com.github.nginx-proxy.nginx-proxy.loadbalance"`
- [x] 1.2 `DockerLabels.NginxSslVerifyClient = "com.github.nginx-proxy.nginx-proxy.ssl_verify_client"`

## 2. Resolve with DockYarp-native precedence + value translation (AG-DD)
- [x] 2.1 `LabelParser.ResolveClientCertificate(labels)`: `DOCKYARP_CLIENT_CERT` first, else translate
      `ssl_verify_client` (`on`→Required, `optional`/`optional_no_ca`→Optional, else None)
- [x] 2.2 `LabelParser.ResolveLoadBalancing(labels)`: `DOCKYARP_LB` first, else `ParsePolicy(ns) ??`
      `TranslateNginxLoadBalance(ns)` (`least_conn`→LeastRequests, `random`, `round_robin`; hashing→null)
- [x] 2.3 Use both resolvers in `TryParse` and `ParseCommon` (replace the direct `DOCKYARP_*` reads)

## 3. Tests (AG-DD)
- [x] 3.1 `ssl_verify_client=optional` → Optional; `=on` → Required; `=off` → None
- [x] 3.2 `loadbalance=least_conn` → LeastRequests; a DockYarp name under the alias key still parses; hashing→null
- [x] 3.3 Precedence: `DOCKYARP_CLIENT_CERT` set alongside the namespaced label → native wins

## 4. Verify (AG-DD)
- [x] 4.1 Nuke `Test` gate green (unit/integration, no Docker) — 314 tests, 0 failures
