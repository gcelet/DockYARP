# yarp-dynamic-config Specification

## Purpose
TBD - created by archiving change add-yarp-dynamic-config. Update Purpose after archive.
## Requirements
### Requirement: Live YARP configuration provider
The system SHALL implement a YARP `IProxyConfigProvider` backed by the `proxy-routing` snapshot store and
SHALL trigger a YARP configuration reload when the snapshot version changes, without restarting the process.

#### Scenario: Snapshot change reloads YARP
- **WHEN** the proxy-routing snapshot version increases after a container change
- **THEN** YARP applies the new routes/clusters without a process restart

### Requirement: Model to YARP mapping
The system SHALL map internal clusters, endpoints, and routes to YARP `ClusterConfig` and `RouteConfig`
objects, preserving host/path matching and cluster membership.

#### Scenario: Route and cluster are proxied
- **WHEN** the active snapshot contains a route for `app.local` targeting a cluster with one endpoint
- **THEN** a request to `app.local` is proxied to that endpoint

### Requirement: Per-cluster load balancing
The system SHALL support at least round-robin and least-requests load-balancing policies, configurable
per cluster.

#### Scenario: Round-robin distributes requests
- **WHEN** a cluster has two healthy endpoints and round-robin is configured
- **THEN** consecutive requests alternate between the two endpoints

### Requirement: Backend health checks
The system SHALL support YARP active and passive health checks per cluster so unhealthy endpoints are not
selected.

#### Scenario: Unhealthy endpoint is excluded
- **WHEN** an endpoint fails its health check
- **THEN** the load balancer stops routing new requests to that endpoint until it recovers

### Requirement: Forwarded headers
The system SHALL set forwarded headers on proxied requests so backends can reconstruct the original
request: `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`, `X-Forwarded-Port`, and `X-Real-IP`,
and SHALL forward an appropriate `Host` header.

#### Scenario: Forwarded headers reach the backend
- **WHEN** a request is proxied to a backend
- **THEN** the backend receives `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`, and `X-Real-IP` reflecting the client and edge

### Requirement: Downstream proxy trust
The system SHALL provide a configurable trust setting that determines whether client-supplied
`X-Forwarded-*` headers are preserved/appended (trusted) or replaced with the connection's own scheme,
host, and port (untrusted).

#### Scenario: Untrusted mode overrides client headers
- **WHEN** downstream proxy trust is disabled and a client sends its own `X-Forwarded-Proto`
- **THEN** the proxied request's forwarded values reflect the actual connection, not the client-supplied header

#### Scenario: Trusted mode preserves upstream headers
- **WHEN** downstream proxy trust is enabled and a trusted upstream sets `X-Forwarded-For`
- **THEN** the proxied request appends to, rather than discards, the existing forwarded chain

### Requirement: Path rewrite transform
When a route defines a path-remove-prefix transform, the system SHALL strip that prefix from the request
path before forwarding to the backend; routes without a transform SHALL forward the original path.

#### Scenario: Prefix stripped before forwarding
- **WHEN** a route for `app.local` with path prefix `/api` sets a path-remove-prefix of `/api` and a request targets `/api/orders`
- **THEN** the backend receives the request path `/orders`

#### Scenario: No transform forwards the original path
- **WHEN** a route defines no path transform and a request targets `/api/orders`
- **THEN** the backend receives `/api/orders` unchanged

### Requirement: Default response for unmatched requests
The system SHALL return a configurable default response for requests that match no route and no default
host — for example a status code (`404`, `503`) or a redirect — instead of a bare not-found.

#### Scenario: Configured default status
- **WHEN** the default response is configured as `503` and a request matches no route
- **THEN** the response status is 503

#### Scenario: Default is 404 when unset
- **WHEN** no default response is configured and a request matches no route
- **THEN** the response status is 404

