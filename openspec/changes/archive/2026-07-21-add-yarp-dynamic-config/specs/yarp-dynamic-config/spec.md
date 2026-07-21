## ADDED Requirements

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
