## Why

nginx-proxy supports client-affinity ("sticky sessions") via its `loadbalance` label (`ip_hash;` or
`hash $remote_addr consistent;`), which routes all requests from one client IP to the same backend. DockYarp
has load-balancing policies (`add-loadbalance-policies`) but no affinity mechanism — a client's requests can
land on any healthy destination on every request. This is a real, commonly-requested nginx-proxy parity gap.
DockYarp goes beyond nginx-proxy's own ceiling here: nginx-proxy is built on open-source nginx, which has no
cookie-based sticky-session mechanism at all (that is an NGINX Plus-only, commercial feature) — but YARP
ships cookie/header-based affinity natively, so DockYarp can offer it too, expanding past strict parity where
it costs little extra.

## What Changes

- New opt-in per-cluster session affinity, selectable via the `DOCKYARP_AFFINITY` label (or the nginx-proxy
  compat `loadbalance` directive for the IP-hash case only — nginx has nothing to translate for the other two):
  - **`ip-hash`** (also `true`): a **custom** YARP `ISessionAffinityPolicy` (`ClientIpHashSessionAffinityPolicy`)
    hashing the client's connection IP — first 3 octets for IPv4 (mirroring nginx's own `ip_hash` algorithm),
    full address for IPv6. Stateless, no cookie, effective from the client's first request. **No Data
    Protection dependency.** This is the true nginx-proxy parity mechanism and the label's default meaning.
  - **`cookie`**: YARP's built-in `Cookie` policy — encrypts the destination-id key via Data Protection.
  - **`custom-header`**: YARP's built-in `CustomHeader` policy — same encryption, via a header instead of a
    cookie.
  - Deliberately **not** exposed now (tracked as a separate backlog item for later, not forgotten): YARP's
    `HashCookie` (its own default) and `ArrCookie` built-in policies — cookie-based but unencrypted (no DP
    dependency). Left out to keep this change's config/test surface contained; nothing about this design
    blocks adding them later.
- **A container requesting `cookie`/`custom-header` without `DataProtection:CertificatePath` configured is
  degraded, not excluded**: consistent with every other per-container "unsupported/invalid config" case
  already in `ContainerMapper.cs` (unrecognized `DOCKYARP_LB`, `HTTPS_METHOD`, etc. — all fall back to a safe
  default and keep the route serving), the cluster's affinity is dropped (falls back to no affinity, ordinary
  load-balancing) rather than the whole route being excluded. Logged at **Error** (not the usual Warning),
  since silently downgrading to unencrypted would defeat the specific security property the operator opted
  into — worth a louder signal than a routine unsupported-value fallback.
- `StaticConfig` JSON path gets an equivalent `Affinity` field on its cluster entry, mirroring the existing
  `LoadBalancing` field, so the feature is reachable from both config sources like every other cluster setting.
- Documentation: `docs-site/content/en/docs/configuration.md` (new label, all 3 values, the DP requirement for
  2 of them) and `docs-site/content/en/docs/migrating-from-nginx-proxy.md` if it already discusses
  `loadbalance`.

## Capabilities

### New Capabilities
(none — this extends the existing dynamic-config capability, no new capability path)

### Modified Capabilities
- `yarp-dynamic-config`: new requirement for per-cluster session affinity — label/compat parsing for 3
  policies, mapping into `Cluster`/`ClusterConfig`, the custom client-IP-hash policy, the Data
  Protection-gated built-in policies, and the graceful-degradation behavior when DP isn't configured.

## Impact

- `src/DockYarp.Docker/Labels/DockerLabels.cs`, `LabelParser.cs`, `ContainerLabelConfig.cs` — new label +
  3-way parsing (including the `TranslateNginxLoadBalance`-adjacent compat translation for `ip-hash` only,
  already stubbed as a comment at `LabelParser.cs:415`).
- `src/DockYarp.Core/Models/` — new `SessionAffinityPolicy` enum (`None`/`ClientIpHash`/`Cookie`/
  `CustomHeader`); `Cluster.cs` gets a `SessionAffinityPolicy` property.
- `src/DockYarp.Docker/Mapping/ContainerMapper.cs` — both `BuildCluster` overloads set the new property.
- `src/DockYarp.App/ReverseProxy/YarpConfigMapper.cs` — `Map`/`BuildCluster` take a `dataProtectionConfigured`
  flag, set `ClusterConfig.SessionAffinity` per policy, downgrade `Cookie`/`CustomHeader` to none when DP isn't
  configured, and return diagnostics for the caller to log.
- `src/DockYarp.App/ReverseProxy/YarpConfigBridge.cs` — injects `DataProtectionOptions` + `ILogger`, passes the
  computed flag into `YarpConfigMapper.Map`, logs any returned diagnostics at Error.
- `src/DockYarp.App/ReverseProxy/` (new file) — `ClientIpHashSessionAffinityPolicy : ISessionAffinityPolicy`,
  registered in DI alongside `AddReverseProxy()`.
- `src/DockYarp.App/StaticConfig/StaticConfigProvider.cs` (+ its JSON schema type) — new `Affinity` field,
  mirroring `LoadBalancing`.
- `src/DockYarp.Security/DataProtectionSetup.cs` — doc comment correction (this change is a DP consumer for 2
  of its 3 policies, but still not a hard-fail-at-startup one — see design.md).
- New backlog item: `add-session-affinity-unencrypted-cookie` (or similar id) — YARP's `HashCookie`/`ArrCookie`
  built-in policies, deferred out of this change's scope, tracked for later.
- Tests: `tests/DockYarp.Docker.Tests` (label parsing), a new area for the custom policy's hashing behavior,
  `YarpConfigMapperTests.cs` (DP-gating + downgrade + diagnostics), and an integration/e2e check (batched per
  this project's testing-pyramid strategy) proving repeated requests from one client IP stick end-to-end.
