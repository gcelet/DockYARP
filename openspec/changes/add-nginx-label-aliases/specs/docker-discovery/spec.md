## ADDED Requirements

### Requirement: nginx-proxy label-namespace aliases
The system SHALL recognize the nginx-proxy namespaced labels
`com.github.nginx-proxy.nginx-proxy.ssl_verify_client` and `com.github.nginx-proxy.nginx-proxy.loadbalance` as
aliases for the corresponding per-route settings, translating their nginx-flavored values: `ssl_verify_client`
`on`→required / `optional`(`_no_ca`)→optional / otherwise none; `loadbalance` a DockYarp policy name or a clean
nginx directive (`least_conn`→least-requests, `random`, `round_robin`) → that load-balancing policy, while a
hashing directive is treated as unset (session affinity is out of scope). When both the DockYarp-native key
(`DOCKYARP_CLIENT_CERT` / `DOCKYARP_LB`) and the namespaced label are present, the **DockYarp-native value SHALL
take precedence**.

#### Scenario: ssl_verify_client alias sets the client-certificate requirement
- **WHEN** a container sets `com.github.nginx-proxy.nginx-proxy.ssl_verify_client=optional`
- **THEN** the route requires an optional client certificate, as if `DOCKYARP_CLIENT_CERT=optional` were set

#### Scenario: loadbalance alias selects the policy
- **WHEN** a container sets `com.github.nginx-proxy.nginx-proxy.loadbalance=least_conn`
- **THEN** the cluster uses the least-requests load-balancing policy

#### Scenario: DockYarp-native key wins over the namespaced label
- **WHEN** a container sets both `DOCKYARP_CLIENT_CERT=required` and
  `com.github.nginx-proxy.nginx-proxy.ssl_verify_client=optional`
- **THEN** the route requires a client certificate (the native value is used)
