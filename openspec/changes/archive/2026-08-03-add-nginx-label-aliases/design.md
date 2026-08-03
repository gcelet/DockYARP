# Design — add-nginx-label-aliases

## Scope (only the two with a per-route target)
The item's mapping table shows only `loadbalance` and `ssl_verify_client` have an existing per-route DockYarp
target. The other six namespaced labels are global settings (`trust-default-cert`, `debug-endpoint`,
`http2/3.enable`) or unimplemented (`keepalive`, `non-get-redirect`) — out of scope, tracked by their own items.

## Resolution + precedence
In `LabelParser`, two small resolvers replace the direct reads, used by both `TryParse` and `ParseCommon`:

```
ResolveLoadBalancing(labels)   = DOCKYARP_LB present ? ParsePolicy(DOCKYARP_LB)
                                 : namespaced present ? (ParsePolicy(ns) ?? TranslateNginxLoadBalance(ns))
                                 : null
ResolveClientCertificate(labels) = DOCKYARP_CLIENT_CERT present ? ParseClientCertificate(it)
                                   : namespaced present ? TranslateNginxSslVerifyClient(it)
                                   : None
```

- **DockYarp-native wins**: the `DOCKYARP_*` key is checked first, so a native value overrides the namespaced
  one. The namespaced label is a pure compatibility fallback. (Env-over-label precedence is already applied by
  `EffectiveConfig` before parsing, so a `DOCKYARP_*` env var still wins over a namespaced label.)
- **loadbalance** tries `ParsePolicy` first (so DockYarp names still work under the alias key), then the nginx
  directive translation.

## Value translation
- `ssl_verify_client`: `on`→Required, `optional`/`optional_no_ca`→Optional, `off`/unknown→None.
- `loadbalance` (nginx directive, trailing `;` and arguments trimmed to the first token): `least_conn`→
  LeastRequests, `random`→Random, `round_robin`→RoundRobin; `ip_hash`/`hash …`→null (session affinity, not a
  policy → `add-session-affinity`); unknown→null (falls back to the cluster default).

## Notes
- No new diagnostics: an unmapped namespaced value simply falls back (default policy / no client cert), matching
  how an unknown `DOCKYARP_*` value already behaves. The existing `Has*` warnings stay on the `DOCKYARP_*` keys.
- Keys are ordinal; the label namespace string is a constant on `DockerLabels`.
