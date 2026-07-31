---
id: add-nginx-label-aliases
capability: docker-discovery
agent: AG-DD
tier: A-structural
priority: medium
status: backlog
nginx-proxy: com.github.nginx-proxy.nginx-proxy.* label namespace
provenance: 2026-07-31 parity re-analysis (split from add-env-var-config)
---

## Why
nginx-proxy's per-vhost **label** settings live under the namespace `com.github.nginx-proxy.nginx-proxy.*`
(`loadbalance`, `keepalive`, `ssl_verify_client`, `http2.enable`, `http3.enable`, `non-get-redirect`,
`trust-default-cert`, `debug-endpoint`). DockYarp exposes the equivalent settings under its own names
(`DOCKYARP_LB`, `DOCKYARP_CLIENT_CERT`, …) or globally. For nginx-proxy compatibility, DockYarp should also
recognize the real nginx-proxy label names where a target exists — while keeping its own `DOCKYARP_*` names.

## nginx-proxy behavior
- Label-only per-vhost knobs (namespaced). Values are nginx-flavored (e.g. `loadbalance=hash $remote_addr;`,
  `ssl_verify_client=optional`).

## DockYarp today
- Reads `DOCKYARP_LB`, `DOCKYARP_CLIENT_CERT` per route; `trust-default-cert`/`debug-endpoint`/`http2`/`http3`
  are **global** settings; `keepalive` and per-vhost `non-get-redirect` are not implemented.

## Mapping (target availability)
| nginx-proxy label | DockYarp target | Per-vhost today? |
|---|---|---|
| `…loadbalance` | `DOCKYARP_LB` | ✅ (value: nginx directive vs policy name — map policy-like values; raw `hash`→affinity is [[add-session-affinity]]) |
| `…ssl_verify_client` | `DOCKYARP_CLIENT_CERT` | ✅ (`on`/`optional` → Required/Optional) |
| `…keepalive` | — | ❌ → [[add-backend-keepalive]] |
| `…http2.enable` / `…http3.enable` | global protocols | ❌ (per-vhost not supported) |
| `…non-get-redirect` | always 308 | ❌ (no knob) |
| `…trust-default-cert` | `Security:TrustDefaultCert` | ❌ global |
| `…debug-endpoint` | admin `/api/resolve` | ❌ global |

## Proposed change (sketch)
- Add a label-name alias step: recognize `com.github.nginx-proxy.nginx-proxy.{loadbalance,ssl_verify_client}`
  and map them to the existing DockYarp per-route settings (with value translation), keeping the `DOCKYARP_*`
  names too. Document that the other six namespaced labels have no per-vhost target yet (they map to global
  settings or unimplemented features tracked by their own items).

## Acceptance criteria (→ scenarios)
- **WHEN** a container sets `com.github.nginx-proxy.nginx-proxy.ssl_verify_client=optional` **THEN** the route
  requires an optional client certificate (same as `DOCKYARP_CLIENT_CERT=optional`).
- **WHEN** a container sets `…loadbalance` with a policy-like value **THEN** the cluster uses that LB policy.
- **WHEN** both a namespaced label and the `DOCKYARP_*` label are set **THEN** a defined precedence applies
  (decide: DockYarp-native wins, documented).

## Notes / risks / references
- Depends on / follows [[add-env-var-config]]. Value translation (nginx directive ↔ DockYarp value) is the
  tricky part; keep it to the two that map cleanly. The six global/unimplemented ones are out of scope here.
