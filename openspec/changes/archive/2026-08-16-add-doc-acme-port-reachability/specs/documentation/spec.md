## MODIFIED Requirements

### Requirement: Application configuration reference
The documentation site SHALL document the proxy's own application configuration — each configuration section
(`Server`, `Docker`, `Tls`, `Security`, `Routing`, `Proxy`, `AccessLog`, `AdminApi`, `Compression`,
`DataProtection`, `Host`) with its keys, their defaults, and their purpose — and SHALL state that any key may be
set via `appsettings.json` or a double-underscore environment variable (for example `Tls__AcceptTermsOfService`).
The `Server` section's documentation SHALL state that ACME HTTP-01 needs port 80 reachable from the certificate
authority, and clients need port 443 reachable, regardless of network topology.

#### Scenario: Each application-configuration section is documented with defaults
- **WHEN** a reader opens the Configuration page's application-configuration reference
- **THEN** every section is listed with its keys, each key's default, and its purpose

#### Scenario: The appsettings-or-environment channel is explained
- **WHEN** a reader consults the application-configuration reference
- **THEN** it states that any key may be set via `appsettings.json` or a `Section__Key` environment variable

#### Scenario: Port reachability is stated explicitly
- **WHEN** a reader consults the `Server` section
- **THEN** it states that ACME HTTP-01 needs port 80 reachable from the certificate authority, and clients need
  port 443 reachable, independent of any particular deployment topology

### Requirement: Worked configuration examples
The documentation site SHALL provide worked, copy-pasteable configuration recipes for common scenarios — at
least a basic virtual host, path routing with a rewrite, multiple ports, configuration via environment
variables, automatic HTTPS, mutual TLS, a per-host TLS policy, Basic Auth, internal-only access, running behind
a load balancer, and a deployment with no host port-remap (for example macvlan or host networking) — each using
real DockYARP labels/environment variables and stating the expected result, built on a base stack shown once.
The base stack SHALL state that the `tecnativa/docker-socket-proxy` pattern is required for the non-root image
to reach the Docker API at all, not an optional hardening choice.

#### Scenario: Recipes use real keys and show the expected result
- **WHEN** a reader opens the examples page for a scenario
- **THEN** a copy-pasteable snippet using real labels/environment variables is shown, with the expected result

#### Scenario: The base stack is shown once
- **WHEN** a reader follows the recipes
- **THEN** the `dockyarp` + socket-proxy base stack is presented once and reused by the individual recipes

#### Scenario: The socket-proxy is stated as required
- **WHEN** a reader reads the base stack's introduction
- **THEN** it states plainly that the socket-proxy is required — the non-root image cannot open the Docker
  socket directly — not phrased as optional hardening

#### Scenario: A no-host-port-remap topology is covered
- **WHEN** a reader deploys DockYARP on a topology with no host port-remap layer (macvlan, host networking, or
  equivalent)
- **THEN** a recipe shows exactly what to change — the `NET_BIND_SERVICE` capability and `Server:HttpPort`/
  `Server:HttpsPort` set to `80`/`443` — with no `ports:` mapping, and explains why the default 8080/8443 ports
  aren't reachable in that topology
