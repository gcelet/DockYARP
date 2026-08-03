## Why
nginx-proxy's per-vhost **label** settings live under the namespace `com.github.nginx-proxy.nginx-proxy.*`.
DockYarp exposes the equivalent per-route settings under its own `DOCKYARP_*` names. For nginx-proxy
compatibility on the **label configuration source**, DockYarp should also recognize the real nginx-proxy label
names where a per-route target already exists — keeping its own `DOCKYARP_*` names as the primary interface.

## What Changes
- Recognize two nginx-proxy namespaced labels as **aliases** for existing DockYarp per-route settings, with
  value translation:
  - `com.github.nginx-proxy.nginx-proxy.ssl_verify_client` → the mutual-TLS requirement (`DOCKYARP_CLIENT_CERT`):
    `on`→required, `optional`/`optional_no_ca`→optional, `off`/other→none.
  - `com.github.nginx-proxy.nginx-proxy.loadbalance` → the load-balancing policy (`DOCKYARP_LB`): DockYarp policy
    names and the clean nginx directives (`least_conn`→least-requests, `random`, `round_robin`) map; hashing
    directives (`ip_hash`/`hash …`) are session affinity, not a policy, and are ignored here (→ `add-session-affinity`).
- **Precedence**: the DockYarp-native key (`DOCKYARP_*`) wins when both are set; the namespaced label is the
  compatibility fallback. Works across the env/label merge (env-over-label is already applied upstream).
- The other six namespaced labels (`keepalive`, `http2.enable`, `http3.enable`, `non-get-redirect`,
  `trust-default-cert`, `debug-endpoint`) map to global settings or unimplemented features and are out of scope
  here (tracked by their own items).

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `docker-discovery`: the load-balancing and client-certificate settings are also read from the nginx-proxy
  namespaced labels (DockYarp-native names take precedence).

## Impact
- **Code**: `DockYarp.Docker` — `DockerLabels` (two namespaced keys); `LabelParser` resolves LB policy and
  client-cert requirement from `DOCKYARP_*` first, then the namespaced label with value translation (both
  `TryParse` and `ParseCommon`).
- **Tests (unit)**: `LabelParser` — `ssl_verify_client` value mapping, `loadbalance` value mapping, and the
  DockYarp-native-wins precedence.
- **Runtime / e2e**: none (label parsing is fully unit-testable).
- **Owning agent**: AG-DD. Resolves `add-nginx-label-aliases`; flips the parity "labels namespace" ⚠️ row.
