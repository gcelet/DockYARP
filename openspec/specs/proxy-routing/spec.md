# proxy-routing Specification

## Purpose
TBD - created by archiving change add-proxy-routing-model. Update Purpose after archive.
## Requirements
### Requirement: Internal route model
The system SHALL represent a route with a host pattern, an optional path prefix, a priority, a target
cluster identifier, and optional request transforms, using types owned by `DockYarp.Core` that are
independent from YARP configuration types.

#### Scenario: Route targets a cluster
- **WHEN** a route with host `app.local` and target cluster `app` is present in the active configuration
- **THEN** the model exposes that route as mapping host `app.local` to cluster id `app`

#### Scenario: Route model carries no YARP types
- **WHEN** the route model is compiled
- **THEN** it depends only on the BCL and no reference to YARP assemblies is required by `DockYarp.Core`

### Requirement: Cluster and endpoint model
The system SHALL represent a cluster identified by a stable id, containing one or more endpoints (each a
destination address), a load-balancing policy, and an optional health-check configuration.

#### Scenario: Cluster with multiple endpoints
- **WHEN** a cluster `app` is defined with endpoints `http://10.0.0.1:8080` and `http://10.0.0.2:8080`
- **THEN** the model exposes cluster `app` with exactly those two endpoints

#### Scenario: Endpoint added to an existing cluster
- **WHEN** a second endpoint is added to cluster `app`
- **THEN** the resulting cluster snapshot contains both the original and the new endpoint

### Requirement: Per-host TLS metadata
The system SHALL allow a route/host to carry TLS metadata (certificate host name, contact email, and an
HTTPS-enforcement flag) so that downstream TLS and security capabilities can consume it without
re-parsing labels.

#### Scenario: Host flagged for a certificate
- **WHEN** a route for host `app.local` declares certificate host `app.local` and email `admin@example.com`
- **THEN** the model exposes that host as requiring a certificate for `app.local` with that contact email

### Requirement: Thread-safe versioned configuration store
The system SHALL provide a configuration store that publishes an immutable snapshot of all routes and
clusters, updates the snapshot atomically, and exposes a monotonically increasing version. Readers MUST
never observe a partially applied update.

#### Scenario: Atomic snapshot swap
- **WHEN** the store is updated with a new set of routes while a reader holds a previously obtained snapshot
- **THEN** the reader's snapshot remains unchanged and a subsequently obtained snapshot reflects the update

#### Scenario: Version increments on update
- **WHEN** the store applies an update that changes routes or clusters
- **THEN** the snapshot version is strictly greater than the version before the update

#### Scenario: No-op update does not churn readers
- **WHEN** the store is asked to apply a set identical to the current snapshot
- **THEN** the published snapshot reference and its version are left unchanged

### Requirement: Host and path matching
The system SHALL select, for a given request host and path, the matching route with the highest priority,
preferring an exact host match over a wildcard subdomain match, and the longest matching path prefix.

#### Scenario: Exact host preferred over wildcard
- **WHEN** routes exist for host `app.local` and for wildcard `*.local`, and a request targets `app.local`
- **THEN** the route for `app.local` is selected

#### Scenario: Longest path prefix wins
- **WHEN** two routes match host `app.local` with path prefixes `/` and `/api`, and a request targets `/api/orders`
- **THEN** the route with path prefix `/api` is selected

#### Scenario: No matching route
- **WHEN** no route matches the request host
- **THEN** matching yields no route and the caller can return a not-found/no-route response

### Requirement: Configuration sources and precedence
The system SHALL build the active configuration by merging routes and clusters from a static
configuration source and from dynamic sources (e.g. Docker discovery), applying a deterministic
precedence, and logging conflicts (e.g. two sources claiming the same host) without discarding the whole
configuration.

#### Scenario: Dynamic source adds a route
- **WHEN** the static configuration is empty and a dynamic source contributes a route for `app.local`
- **THEN** the active configuration contains the route for `app.local`

#### Scenario: Conflicting host is resolved and logged
- **WHEN** the static source and a dynamic source both define a route for host `app.local`
- **THEN** the configured precedence decides the winner and the conflict is logged with both sources identified

#### Scenario: One invalid source entry does not drop the rest
- **WHEN** a dynamic source contributes one invalid route and several valid ones
- **THEN** the invalid entry is skipped and logged while the valid routes are still applied

### Requirement: Configuration change notification
The route configuration store SHALL notify observers when it publishes a new snapshot, and SHALL NOT
notify them when an update is a no-op (identical content). The notification enables consumers such as the
YARP integration to reload without polling.

#### Scenario: Observers notified on content change
- **WHEN** an update changes the published routes or clusters
- **THEN** registered observers are notified after the new snapshot becomes current

#### Scenario: No notification on a no-op update
- **WHEN** an update is applied whose content is identical to the current snapshot
- **THEN** no change notification is raised

### Requirement: Route authentication metadata
The routing model SHALL allow a route to carry optional Basic Auth credentials (username, password, and an
optional realm) so the security capability can protect it. The routing model only stores this metadata; it
performs no authentication itself.

#### Scenario: Route carries Basic Auth credentials
- **WHEN** a route is configured with a username and password
- **THEN** the route exposes those credentials (and optional realm) for the security layer to enforce

#### Scenario: Route without credentials is unprotected
- **WHEN** a route is configured without auth credentials
- **THEN** the route exposes no credentials and is not protected

### Requirement: Backend scheme on endpoints
The routing model SHALL allow a cluster endpoint to carry a backend scheme (at least `http` and `https`),
defaulting to `http`, so downstream proxying can target HTTPS backends.

#### Scenario: Endpoint exposes its scheme
- **WHEN** an endpoint is created with scheme `https`
- **THEN** the endpoint's address targets the backend over HTTPS

#### Scenario: Default scheme is http
- **WHEN** an endpoint is created without an explicit scheme
- **THEN** the endpoint targets the backend over HTTP

