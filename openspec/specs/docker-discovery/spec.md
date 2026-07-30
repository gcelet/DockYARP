# docker-discovery Specification

## Purpose
TBD - created by archiving change add-docker-discovery. Update Purpose after archive.
## Requirements
### Requirement: Docker events subscription
The system SHALL subscribe to the Docker daemon event stream and react to container `start`, `stop`,
`die`, and `update` events by updating the dynamic configuration accordingly.

#### Scenario: Labeled container starts
- **WHEN** a container carrying `VIRTUAL_HOST=app.local` is started
- **THEN** discovery creates/updates the corresponding route and cluster endpoint for `app.local`

#### Scenario: Container stops
- **WHEN** a running, routed container stops or dies
- **THEN** discovery removes that container's endpoint from its cluster

### Requirement: Resilient event connection
The system SHALL maintain the Docker event subscription across daemon restarts and transient connection
failures by reconnecting automatically, and SHALL reconcile state after reconnecting so no change is lost.

#### Scenario: Daemon restarts
- **WHEN** the Docker daemon connection drops and later becomes available again
- **THEN** discovery reconnects to the event stream without operator intervention

#### Scenario: Reconcile after reconnect
- **WHEN** discovery reconnects after an outage during which containers changed
- **THEN** it re-enumerates running containers so the active configuration matches reality

### Requirement: Startup reconciliation
The system SHALL, at startup, enumerate already-running containers and build the initial configuration
from their labels, independently of any future events.

#### Scenario: Container started before DockYarp
- **WHEN** a labeled container is already running when DockYarp starts
- **THEN** discovery routes it during startup without waiting for a new Docker event

### Requirement: Label schema and parsing
The system SHALL parse nginx-proxy-compatible labels (`VIRTUAL_HOST`, `VIRTUAL_PORT`, `VIRTUAL_PATH`,
`LETSENCRYPT_HOST`, `LETSENCRYPT_EMAIL`) and `DOCKYARP_*` labels into a strongly-typed configuration
object, applying documented defaults (e.g. a default target port when `VIRTUAL_PORT` is absent).

#### Scenario: Host and port produce a routable target
- **WHEN** a container declares `VIRTUAL_HOST=app.local` and `VIRTUAL_PORT=8080`
- **THEN** the parsed configuration targets that container on port 8080 for host `app.local`

#### Scenario: Default port when VIRTUAL_PORT is absent
- **WHEN** a container declares `VIRTUAL_HOST=app.local` with no `VIRTUAL_PORT` and exposes a single port
- **THEN** the parsed configuration targets that single exposed port

### Requirement: Label validation with safe fallback
The system SHALL validate label combinations and, on invalid or conflicting labels, log a structured
error and ignore the offending container without crashing or affecting other containers.

#### Scenario: Invalid label combination is ignored
- **WHEN** a container declares `VIRTUAL_PORT` but no `VIRTUAL_HOST`
- **THEN** the container is skipped, a structured warning is logged, and other containers remain routed

#### Scenario: One bad container does not stop discovery
- **WHEN** one container has invalid labels among several valid ones
- **THEN** the valid containers are still routed and only the invalid one is skipped and logged

### Requirement: Mapping to the routing model
The system SHALL translate parsed container configuration and container network information into
`proxy-routing` routes, clusters, endpoints, and per-host TLS metadata, and publish them as the dynamic
configuration source so the active snapshot updates without a process restart.

#### Scenario: Second replica joins the cluster
- **WHEN** a second container with the same `VIRTUAL_HOST` is started
- **THEN** its endpoint is added to the existing cluster for that host rather than creating a new route

#### Scenario: LETSENCRYPT labels populate TLS metadata
- **WHEN** a container declares `LETSENCRYPT_HOST=app.local` and `LETSENCRYPT_EMAIL=admin@example.com`
- **THEN** the mapped route carries TLS metadata requesting a certificate for `app.local` with that email

### Requirement: Basic Auth labels
The system SHALL read `DOCKYARP_AUTH_USER`, `DOCKYARP_AUTH_PASSWORD`, and optional
`DOCKYARP_AUTH_REALM` from a container's labels and populate the route's Basic Auth credentials, so the
security layer can protect the route. Incomplete auth labels SHALL be logged and leave the route
unprotected without failing discovery.

#### Scenario: Auth labels protect the route
- **WHEN** a container declares `DOCKYARP_AUTH_USER=admin` and `DOCKYARP_AUTH_PASSWORD=secret`
- **THEN** the mapped route carries those Basic Auth credentials (with the realm if provided)

#### Scenario: Incomplete auth labels are ignored safely
- **WHEN** a container declares `DOCKYARP_AUTH_USER` but no `DOCKYARP_AUTH_PASSWORD`
- **THEN** the route is left unprotected and a warning is logged, and other containers are unaffected

### Requirement: Multiple hosts per container
The system SHALL accept a comma-separated `VIRTUAL_HOST` and map the container to one route per host, each
sharing the container's port, path, TLS, and auth settings. Empty entries SHALL be ignored, and a repeated
host SHALL be de-duplicated (case-insensitive).

#### Scenario: Comma-separated hosts create multiple routes
- **WHEN** a container declares `VIRTUAL_HOST=app.local,www.app.local`
- **THEN** routes are created for both `app.local` and `www.app.local` targeting the container

#### Scenario: Whitespace and empty entries are tolerated
- **WHEN** a container declares `VIRTUAL_HOST=a.local, ,b.local`
- **THEN** routes are created for `a.local` and `b.local` and the empty entry is ignored

#### Scenario: Repeated host is de-duplicated
- **WHEN** a container declares `VIRTUAL_HOST=app.local,app.local`
- **THEN** a single route/cluster is created for `app.local`

### Requirement: VIRTUAL_PROTO backend scheme
The system SHALL read `VIRTUAL_PROTO` from a container's labels and use it as the backend scheme (`http`
or `https`), defaulting to `http` when absent and falling back to `http` (with a warning) for unsupported
values.

#### Scenario: HTTPS backend
- **WHEN** a container declares `VIRTUAL_PROTO=https` and `VIRTUAL_PORT=443`
- **THEN** the mapped endpoint targets the container over HTTPS on port 443

#### Scenario: Unsupported value falls back to http
- **WHEN** a container declares `VIRTUAL_PROTO=fastcgi` (not yet supported)
- **THEN** the endpoint targets HTTP and a warning is logged

### Requirement: VIRTUAL_DEST path rewrite
The system SHALL read `VIRTUAL_DEST` from a container's labels and configure the route's path rewrite so
the backend receives the rewritten path. When `VIRTUAL_DEST` is absent, no rewrite is configured.

#### Scenario: VIRTUAL_DEST strips the path prefix
- **WHEN** a container declares `VIRTUAL_PATH=/api` and `VIRTUAL_DEST=/`
- **THEN** the mapped route strips `/api` before forwarding, so `/api/x` reaches the backend as `/x`

#### Scenario: No VIRTUAL_DEST keeps the path
- **WHEN** a container declares `VIRTUAL_PATH=/api` and no `VIRTUAL_DEST`
- **THEN** the mapped route forwards the original path unchanged

### Requirement: Health-aware endpoint selection
The system SHALL exclude a container from routing while its Docker health status is `unhealthy` or
`starting`, and SHALL route to a container that is `healthy` or declares no health check. When several
containers share a host, an excluded container SHALL NOT remove the healthy siblings from the cluster.

#### Scenario: Healthy container is routed
- **WHEN** a container declaring `VIRTUAL_HOST=app.local` reports health `healthy`
- **THEN** its endpoint is added to the `app.local` cluster

#### Scenario: Unhealthy container is excluded
- **WHEN** a container declaring `VIRTUAL_HOST=app.local` reports health `unhealthy`
- **THEN** no endpoint is added for it and the exclusion is reported

#### Scenario: Container without a health check is routed
- **WHEN** a container declaring `VIRTUAL_HOST=app.local` has no health check
- **THEN** its endpoint is added to the `app.local` cluster

#### Scenario: Healthy sibling still serves the host
- **WHEN** two containers share `VIRTUAL_HOST=app.local` and one is `unhealthy` while the other is `healthy`
- **THEN** the `app.local` cluster contains only the healthy container's endpoint

### Requirement: React to health transitions
The system SHALL treat a Docker `health_status` event as a trigger to re-evaluate discovery, so a container
that becomes healthy is added and one that becomes unhealthy is removed.

#### Scenario: Health-status event triggers reconciliation
- **WHEN** the Docker daemon emits a `health_status` event for a container
- **THEN** discovery re-evaluates the running containers and updates the routing configuration

### Requirement: Network address selection
The system SHALL select the container address to forward to from the container's Docker networks: when a
preferred network is configured and the container is attached to it, that network's IP SHALL be used;
otherwise the system SHALL choose deterministically among the container's networks and SHALL skip the Swarm
`ingress` network. When the proxy's own networks are configured (`Docker:ProxyNetworks`), the deterministic
choice SHALL be restricted to networks the proxy shares (reachable), and a container reachable on no shared
network SHALL be skipped with a warning rather than routed to an unreachable address. When the proxy's networks
are not configured, the system SHALL fall back to the container name when no network address is available.

#### Scenario: Preferred network is used
- **WHEN** a container is attached to `frontend` (10.0.1.2) and `backend` (10.0.2.2) and the preferred network is `backend`
- **THEN** the forwarded address is `10.0.2.2`

#### Scenario: Swarm ingress network is skipped
- **WHEN** a container is attached to `ingress` (10.0.0.5) and `app` (10.0.1.5) and no preferred network is configured
- **THEN** the forwarded address is `10.0.1.5`

#### Scenario: Selection is deterministic
- **WHEN** a container is attached to several networks with no preferred network configured
- **THEN** the same network's IP is chosen on every reconciliation (ordinal by network name)

#### Scenario: Shared reachable network is selected across multiple networks
- **WHEN** a container is attached to several networks, no preferred network is configured, and
  `Docker:ProxyNetworks` lists one of those networks
- **THEN** the forwarded address is the container's IP on that shared, reachable network

#### Scenario: Backend on no reachable network is skipped
- **WHEN** a container is attached only to networks absent from `Docker:ProxyNetworks`
- **THEN** it has no forwarded address and is skipped with a warning (no broken route or endpoint)

### Requirement: Priority label
The system SHALL read `DOCKYARP_PRIORITY` from a container's labels and use it as the route's priority,
defaulting to `0` when absent and falling back to `0` (with a warning) for a non-numeric value.

#### Scenario: Priority label sets the route priority
- **WHEN** a container declares `DOCKYARP_PRIORITY=10`
- **THEN** the mapped route has priority `10`

#### Scenario: Non-numeric priority falls back to zero
- **WHEN** a container declares `DOCKYARP_PRIORITY=high`
- **THEN** the mapped route has priority `0` and the invalid value is reported

### Requirement: HTTPS method label
The system SHALL read `HTTPS_METHOD` from a container's labels and set the route's HTTPS method
(`redirect` (default), `noredirect`, `nohttp`, `nohttps`) on its TLS metadata, defaulting to `redirect`
when absent and falling back to `redirect` (with a warning) for an unrecognized value. The method applies to
a host that carries TLS metadata (a certificate host).

#### Scenario: HTTPS_METHOD selects the redirect policy
- **WHEN** a container declares `LETSENCRYPT_HOST=app.local` and `HTTPS_METHOD=noredirect`
- **THEN** the mapped route's TLS metadata carries the `noredirect` method

#### Scenario: Unrecognized value falls back to redirect
- **WHEN** a container declares `LETSENCRYPT_HOST=app.local` and `HTTPS_METHOD=bogus`
- **THEN** the route's TLS metadata uses `redirect` and the invalid value is reported

### Requirement: HSTS label
The system SHALL read an `HSTS` label and carry it as the route's per-host HSTS policy on its TLS metadata:
a value sets the `Strict-Transport-Security` header for the host, and `off` disables HSTS for the host. When
absent, the global HSTS policy applies.

#### Scenario: HSTS label sets a per-host policy
- **WHEN** a container declares `LETSENCRYPT_HOST=app.local` and `HSTS=off`
- **THEN** the mapped route's TLS metadata carries the `off` HSTS policy

### Requirement: Client certificate label
The system SHALL read `DOCKYARP_CLIENT_CERT` from a container's labels and set the route's
client-certificate requirement (`required`, `optional`, or `none`/`off`), defaulting to `none` when absent
and falling back to `none` (with a warning) for an unrecognized value.

#### Scenario: Client certificate label sets the requirement
- **WHEN** a container declares `DOCKYARP_CLIENT_CERT=required`
- **THEN** the mapped route requires a client certificate

#### Scenario: Unrecognized value falls back to none
- **WHEN** a container declares `DOCKYARP_CLIENT_CERT=maybe`
- **THEN** the route requires no client certificate and the invalid value is reported

### Requirement: Proxy tuning labels
The system SHALL read `DOCKYARP_PROXY_TIMEOUT` (a request timeout in seconds) into the cluster's request
timeout and `DOCKYARP_MAX_BODY_SIZE` (a maximum request body size in bytes) into the route's body-size
limit. Absent labels leave the values unset; a non-numeric or non-positive value is ignored (with a
warning).

#### Scenario: Proxy timeout label sets the cluster timeout
- **WHEN** a container declares `DOCKYARP_PROXY_TIMEOUT=30`
- **THEN** the mapped cluster's request timeout is 30 seconds

#### Scenario: Max body size label sets the route limit
- **WHEN** a container declares `DOCKYARP_MAX_BODY_SIZE=1048576`
- **THEN** the mapped route's maximum request body size is 1048576 bytes

#### Scenario: Invalid value is ignored
- **WHEN** a container declares `DOCKYARP_PROXY_TIMEOUT=soon`
- **THEN** the cluster has no request timeout and the invalid value is reported

### Requirement: Multiple host/path/port mappings per container
The system SHALL read `VIRTUAL_HOST_MULTIPORTS` (a YAML mapping of host → path → `{ port, proto, dest }`) and
map each entry to a route matching that host and path and a cluster targeting the container on the entry's
port and scheme (default `http`). Container-level attributes (auth, load balancing, priority, timeout, body
size, client-certificate requirement, and TLS when the host is a `LETSENCRYPT_HOST`) SHALL apply to the
generated routes. Invalid YAML SHALL be ignored with a warning. A container without the label keeps the
classic `VIRTUAL_HOST` mapping.

#### Scenario: Multiple ports on one host
- **WHEN** a container declares `VIRTUAL_HOST_MULTIPORTS` mapping `app.local` `/` → port 8080 and `/api` → port 9000
- **THEN** two routes are created for `app.local` and `app.local/api`, the latter targeting the container on port 9000

#### Scenario: Multiple hosts
- **WHEN** a container declares multiports entries for `a.local` and `b.local`
- **THEN** routes are created for both hosts

#### Scenario: Invalid YAML is ignored
- **WHEN** a container declares a `VIRTUAL_HOST_MULTIPORTS` value that is not valid YAML
- **THEN** no multiports routes are created for it and the problem is reported

### Requirement: Container discovery filters
The system SHALL support restricting the set of discovered containers using Docker-native inclusion filters,
configured via `Docker:ContainerFilters` as a map of Docker filter key (e.g. `label`, `name`, `network`) to
one or more values. Values within one key SHALL be OR-combined and distinct keys SHALL be AND-combined (Docker
filter semantics). The filter SHALL be applied to the authoritative container listing used by every
reconciliation pass, so only containers matching the configured filters are considered for routing. When no
filter is configured, discovery SHALL consider all running containers, unchanged from prior behavior.

#### Scenario: Label filter restricts the discovered set
- **WHEN** `Docker:ContainerFilters` restricts discovery to `label=dockyarp.enable=true`
- **THEN** the container listing request carries that Docker filter and only containers with that label are
  considered for routing

#### Scenario: Filtered-out container yields no routing change
- **WHEN** a container that does not match the configured filters starts or stops
- **THEN** reconciliation against the filtered listing produces no route or cluster change for that container

#### Scenario: No filter preserves discovery
- **WHEN** no `Docker:ContainerFilters` is configured
- **THEN** discovery considers all running containers, unchanged from prior behavior

### Requirement: Host-network backends
The system SHALL support backends running in Docker host network mode. A host-network container (identified by
a reserved `host` network entry) has no container IP, so the system SHALL forward to the configured Docker host
address (`Docker:HostAddress`) on the backend's port. A host-network backend SHALL require `VIRTUAL_PORT`
(no port can be inferred). When `Docker:HostAddress` is not configured, a host-network backend SHALL be skipped
with a warning rather than routed.

#### Scenario: Host-network backend is routed to the host address
- **WHEN** a host-network container sets `VIRTUAL_HOST` and `VIRTUAL_PORT` and `Docker:HostAddress` is configured
- **THEN** its cluster targets the configured host address on that port

#### Scenario: Host-network backend without a host address is skipped
- **WHEN** a host-network container is discovered and `Docker:HostAddress` is not configured
- **THEN** it is not routed and a warning indicates `Docker:HostAddress` must be set

#### Scenario: Host-network backend without VIRTUAL_PORT is skipped
- **WHEN** a host-network container sets `VIRTUAL_HOST` but no `VIRTUAL_PORT` (and exposes no single port)
- **THEN** it is not routed and a warning indicates `VIRTUAL_PORT` is required

