## ADDED Requirements

### Requirement: Per-cluster session affinity
The system SHALL support opt-in client-affinity ("sticky sessions") per cluster, configurable via the
`DOCKYARP_AFFINITY` label with one of three values: `ip-hash` (also `true`), `cookie`, or `custom-header`.
When `DOCKYARP_AFFINITY` is absent, the system SHALL fall back to translating the nginx-proxy compatibility
`loadbalance` directive (`com.github.nginx-proxy.nginx-proxy.loadbalance`): a value of `ip_hash` or a `hash`
directive (with or without arguments) SHALL be treated as `ip-hash`; no other `loadbalance` value SHALL be
translated to any affinity policy (nginx has no equivalent for `cookie`/`custom-header`, since open-source
nginx — which nginx-proxy is built on — has no cookie-based sticky-session mechanism at all). When affinity is
not enabled, cluster behavior SHALL be unchanged (destinations selected purely by the configured
load-balancing policy, as today). The system SHALL also support enabling affinity via `StaticConfig`'s
per-cluster `Affinity` field, with the same three values, reachable the same way as the Docker label path.

#### Scenario: Enabled via the native label
- **WHEN** a container sets `DOCKYARP_AFFINITY=ip-hash` (or `true`)
- **THEN** its cluster uses client-IP-hash affinity

#### Scenario: Enabled via the nginx-proxy compat label
- **WHEN** a container sets `com.github.nginx-proxy.nginx-proxy.loadbalance` to `ip_hash;` or
  `hash $remote_addr consistent;`, and `DOCKYARP_AFFINITY` is not set
- **THEN** its cluster uses client-IP-hash affinity

#### Scenario: Native label takes precedence over the compat label
- **WHEN** a container sets both `DOCKYARP_AFFINITY=false` and a `loadbalance` directive of `ip_hash;`
- **THEN** affinity is disabled for that cluster (the native label wins, matching the existing
  `DOCKYARP_LB`/`loadbalance` precedence pattern)

#### Scenario: Disabled by default, no behavior change
- **WHEN** a cluster declares no `DOCKYARP_AFFINITY` and no `ip_hash`/`hash` `loadbalance` directive
- **THEN** its destinations are selected purely by the configured load-balancing policy, unchanged from today

#### Scenario: Reachable from static configuration
- **WHEN** a `StaticConfig` cluster entry sets its `Affinity` field to `cookie`
- **THEN** that cluster uses the cookie-based affinity policy, the same as the Docker label path

### Requirement: Client-IP-hash affinity policy
The `ip-hash` policy SHALL deterministically select the same destination for requests carrying the same client
IP, using a hash of the first 3 octets of an IPv4 address (or the full address for IPv6) — matching nginx's own
`ip_hash` algorithm, so that clients from the same dynamic-IP /24 subnet remain stable on one destination.
This mechanism SHALL require no cookie, no response header, and no client-side state: it SHALL have an effect
from a client's very first request, and SHALL NOT depend on Data Protection or any encrypted payload.

#### Scenario: Repeated requests from one client stick to one destination
- **WHEN** `ip-hash` affinity is enabled for a cluster with multiple healthy destinations and a client sends
  several requests from the same IP address
- **THEN** every request is routed to the same destination

#### Scenario: Clients in the same /24 subnet stay stable
- **WHEN** `ip-hash` affinity is enabled and two IPv4 clients share the same first 3 octets
- **THEN** both are routed to the same destination

#### Scenario: Different clients may land on different destinations
- **WHEN** `ip-hash` affinity is enabled and two clients have IP addresses hashing to different destinations
- **THEN** each client is consistently routed to its own selected destination, independent of the other's

#### Scenario: The failed destination's affinity is redistributed, not fatal
- **WHEN** `ip-hash` affinity is enabled and a request's previously-selected destination is no longer healthy
- **THEN** the request is redistributed to another healthy destination via the cluster's load-balancing
  policy, rather than the request failing

### Requirement: Cookie and custom-header affinity require Data Protection, degrading gracefully when absent
The `cookie` and `custom-header` policies encrypt the affinity key via ASP.NET Core Data Protection (YARP's
built-in `Cookie`/`CustomHeader` policies). When a cluster requests one of these two policies and
`DataProtection:CertificatePath` is not configured, the system SHALL NOT apply that affinity policy — the
cluster SHALL be served exactly as if no affinity were configured (ordinary load-balancing, route otherwise
unaffected) — and SHALL report the situation at Error severity, distinct from the Warning severity used for
other unsupported/invalid per-container configuration, since silently downgrading to an unencrypted or
unprotected cookie/header would defeat the security property the operator explicitly opted into.

#### Scenario: Cookie affinity applied when Data Protection is configured
- **WHEN** a cluster requests `cookie` affinity and `DataProtection:CertificatePath` is configured
- **THEN** the cluster uses YARP's `Cookie` session affinity policy

#### Scenario: Cookie affinity degrades gracefully without Data Protection
- **WHEN** a cluster requests `cookie` or `custom-header` affinity and `DataProtection:CertificatePath` is not
  configured
- **THEN** no affinity is applied to that cluster, its route continues to serve requests normally via ordinary
  load-balancing, and an Error-level diagnostic identifies the affected cluster and the missing requirement

#### Scenario: Other clusters are unaffected
- **WHEN** one cluster's `cookie`/`custom-header` affinity is degraded due to missing Data Protection
  configuration
- **THEN** every other cluster's routing and affinity configuration is unaffected
