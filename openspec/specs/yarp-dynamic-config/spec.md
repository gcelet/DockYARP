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
The system SHALL support YARP's built-in load-balancing policies, configurable per cluster via `DOCKYARP_LB`:
round-robin (default), least-requests, power-of-two-choices, random, and first-alphabetical. An unrecognized
`DOCKYARP_LB` value SHALL fall back to round-robin and SHALL be reported as a warning.

#### Scenario: Round-robin distributes requests
- **WHEN** a cluster has two healthy endpoints and round-robin is configured
- **THEN** consecutive requests alternate between the two endpoints

#### Scenario: A built-in policy is selected
- **WHEN** a container sets `DOCKYARP_LB=power-of-two-choices`
- **THEN** its cluster uses YARP's power-of-two-choices policy

#### Scenario: Unknown policy falls back with a warning
- **WHEN** a container sets an unrecognized `DOCKYARP_LB` value
- **THEN** the cluster uses round-robin and a warning is reported

### Requirement: Backend health checks
The system SHALL support YARP active and passive health checks per cluster so unhealthy endpoints are not
selected.

#### Scenario: Unhealthy endpoint is excluded
- **WHEN** an endpoint fails its health check
- **THEN** the load balancer stops routing new requests to that endpoint until it recovers

### Requirement: Forwarded headers
The system SHALL set forwarded headers on proxied requests so backends can reconstruct the original
request: `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`, `X-Forwarded-Port`, `X-Real-IP`,
`X-Forwarded-Ssl` (`on` when the effective forwarded proto is HTTPS, else `off`), and `X-Original-URI` (the
original request URI: path base + path + query), and SHALL forward an appropriate `Host` header.
`X-Forwarded-Ssl` SHALL mirror `X-Forwarded-Proto` (respecting downstream-proxy trust); `X-Original-URI` SHALL
always be the real original URI (a client-supplied value is replaced).

For a route with a `required` or `optional` client-certificate requirement, the system SHALL forward the
connection's client-certificate verification outcome to the backend as `X-SSL-Client-Verify`: `SUCCESS` when a
presented certificate chains to the configured CA and is not revoked, `FAILED` when a certificate was presented
but does not chain to the CA or is revoked, or `NONE` when no certificate was presented. `X-SSL-Client-S-DN`
(the certificate subject) and `X-SSL-Client-I-DN` (the certificate issuer) SHALL be forwarded only alongside
`SUCCESS`. For a route with no client-certificate requirement, no `X-SSL-Client-*` header SHALL reach the
backend. The system SHALL always strip any client-supplied `X-SSL-Client-*` headers first, so the backend only
ever receives DockYarp-set values.

#### Scenario: Forwarded headers reach the backend
- **WHEN** a request is proxied to a backend
- **THEN** the backend receives `X-Forwarded-For`, `X-Forwarded-Proto`, `X-Forwarded-Host`, and `X-Real-IP` reflecting the client and edge

#### Scenario: SSL and original-URI headers reach the backend
- **WHEN** a request is proxied to a backend
- **THEN** the backend receives `X-Forwarded-Ssl` (`on`/`off` matching the forwarded proto) and `X-Original-URI`
  carrying the original request path and query

#### Scenario: Verified client certificate is forwarded
- **WHEN** a request is proxied over a connection carrying a verified client certificate on a `required` or
  `optional` route
- **THEN** the backend receives `X-SSL-Client-Verify: SUCCESS` with the certificate's subject and issuer

#### Scenario: Untrusted or revoked client certificate is reported as FAILED
- **WHEN** a request is proxied over a connection carrying a client certificate that does not chain to the
  configured CA, or is revoked, on an `optional` route
- **THEN** the backend receives `X-SSL-Client-Verify: FAILED` with no subject/issuer headers, and the request is
  not rejected

#### Scenario: Missing client certificate is reported as NONE
- **WHEN** a request is proxied over a connection with no client certificate on an `optional` route
- **THEN** the backend receives `X-SSL-Client-Verify: NONE` with no subject/issuer headers

#### Scenario: Route without a client-certificate requirement gets no SSL-client headers
- **WHEN** a request is proxied to a route with no client-certificate requirement
- **THEN** the backend receives no `X-SSL-Client-*` header

#### Scenario: Client-supplied client-cert headers are stripped
- **WHEN** a client sends its own `X-SSL-Client-Verify`/`X-SSL-Client-S-DN` header
- **THEN** those headers are stripped from the proxied request before any DockYarp-set value is applied

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
When a route defines a path-remove-prefix transform, the system SHALL strip that prefix from the request path
before forwarding to the backend. When a route also defines a path-add-prefix (an arbitrary `VIRTUAL_DEST`
destination), the system SHALL prepend that prefix after stripping, rewriting the matched prefix to the
destination. A destination of `/` (or empty) keeps the pure strip behavior. Routes without a transform SHALL
forward the original path.

#### Scenario: Prefix stripped before forwarding
- **WHEN** a route for `app.local` with path prefix `/api` sets a path-remove-prefix of `/api` and a request targets `/api/orders`
- **THEN** the backend receives the request path `/orders`

#### Scenario: Prefix rewritten to a destination
- **WHEN** a route with path prefix `/api` sets a path-remove-prefix of `/api` and a path-add-prefix of `/v2`, and a request targets `/api/orders`
- **THEN** the backend receives the request path `/v2/orders`

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

### Requirement: Route priority ordering
The system SHALL map a route's priority to YARP's route order so that a higher priority takes precedence
(YARP treats a lower order as higher precedence); a priority of `0` leaves the route at YARP's default
order.

#### Scenario: Higher priority yields higher precedence
- **WHEN** a route has priority `5`
- **THEN** the mapped YARP route order is `-5` (higher precedence than a priority-`0` route)

#### Scenario: Default priority keeps the default order
- **WHEN** a route has priority `0`
- **THEN** the mapped YARP route leaves the order unset

### Requirement: Cluster request timeout
When a cluster defines a request timeout, the system SHALL apply it as the YARP activity timeout for that
cluster's outgoing requests; clusters without one keep the default.

#### Scenario: Cluster timeout maps to YARP
- **WHEN** a cluster defines a request timeout of 30 seconds
- **THEN** the mapped YARP cluster's activity timeout is 30 seconds

### Requirement: Per-cluster backend connection limit
When a cluster defines a maximum number of backend connections (`DOCKYARP_MAX_CONNECTIONS`), the system SHALL
apply it as the YARP cluster's `HttpClientConfig.MaxConnectionsPerServer`; a cluster without one keeps YARP's
default backend connection pooling. An invalid (non-positive) value SHALL be ignored with a warning, leaving
the default.

#### Scenario: Connection limit maps to YARP
- **WHEN** a cluster defines `DOCKYARP_MAX_CONNECTIONS=64`
- **THEN** the mapped YARP cluster's `HttpClientConfig.MaxConnectionsPerServer` is 64

#### Scenario: No limit leaves pooling unchanged
- **WHEN** a cluster defines no `DOCKYARP_MAX_CONNECTIONS`
- **THEN** the mapped YARP cluster has no `HttpClient` override and uses YARP's default pooling

#### Scenario: Invalid value is ignored
- **WHEN** a cluster defines `DOCKYARP_MAX_CONNECTIONS=0` or a non-numeric value
- **THEN** the value is ignored, a warning is reported, and the cluster keeps YARP's default pooling

### Requirement: Per-route request body-size limit
When a route defines a maximum request body size, the system SHALL apply that limit to the request before it
is proxied; routes without one are unaffected. When the request declares a `Content-Length` that exceeds the
limit, the system SHALL reject it with 413 **before proxying** (without opening a backend connection); an
over-limit body sent without a declared `Content-Length` SHALL still be rejected during the read.

#### Scenario: Body-size limit is applied
- **WHEN** a request matches a route with a maximum request body size
- **THEN** the request's maximum body size is set to that limit before proxying

#### Scenario: No limit leaves the request unchanged
- **WHEN** a request matches a route with no maximum request body size
- **THEN** the request's maximum body size is left unchanged

#### Scenario: Declared oversized body is rejected before proxying
- **WHEN** a request matches a route with a maximum body size and declares a `Content-Length` above it
- **THEN** the response status is 413 and the request is not proxied to a backend

#### Scenario: Undeclared oversized body is rejected during the read
- **WHEN** a request to a route with a maximum body size exceeds it but declares no `Content-Length`
- **THEN** the request is still rejected with 413

### Requirement: Custom error pages
When a custom error page named `{statusCode}.html` is configured, the system SHALL write it as the response
body for a DockYarp-generated error response (status ≥ 400) that has not yet started and has no body;
responses already produced (for example streamed by a backend) SHALL be left unchanged, and when no page is
configured for the status the response is unchanged.

#### Scenario: Configured page is served
- **WHEN** a `404.html` page is configured and a request produces a bodiless 404
- **THEN** the response body is the configured page with content type `text/html`

#### Scenario: No page configured leaves the response unchanged
- **WHEN** no page is configured for the response's status code
- **THEN** the response body is left unchanged

### Requirement: Cluster endpoint de-duplication
When mapping a cluster to YARP, the system SHALL de-duplicate destinations by endpoint id (last definition
wins), so duplicate endpoints — for example from a repeated host in `VIRTUAL_HOST` or a repeated static
address — never fail configuration.

#### Scenario: Duplicate endpoints collapse to one destination
- **WHEN** a cluster contains two endpoints with the same id
- **THEN** the mapped YARP cluster has a single destination and mapping does not fail

### Requirement: Strip the inbound Proxy header
The system SHALL remove a client-supplied `Proxy` request header before forwarding a request to a backend, to
mitigate the httpoxy vulnerability.

#### Scenario: Client-supplied Proxy header is not forwarded
- **WHEN** a client sends a request carrying a `Proxy` header
- **THEN** the backend receives the request without a `Proxy` header

### Requirement: Response compression
The system SHALL compress responses (gzip and brotli) for compressible content types when the client indicates
support via `Accept-Encoding`. Compression SHALL be enabled by default and SHALL be disableable by
configuration. The system SHALL NOT compress a response that already carries a `Content-Encoding` (it does not
double-compress an upstream-encoded body).

#### Scenario: Compressible response is compressed
- **WHEN** compression is enabled and a client sends `Accept-Encoding: gzip` for a compressible text response
  with no upstream `Content-Encoding`
- **THEN** the response is returned with `Content-Encoding: gzip`

#### Scenario: Already-encoded response is not re-compressed
- **WHEN** a response already carries a `Content-Encoding`
- **THEN** the system forwards it without adding another compression layer

#### Scenario: Compression disabled by configuration
- **WHEN** compression is disabled by configuration
- **THEN** responses are returned without a `Content-Encoding` added by the proxy

### Requirement: gRPC backend protocol
The system SHALL support declaring a backend as gRPC via `VIRTUAL_PROTO=grpc` (plaintext/HTTP-2) or
`VIRTUAL_PROTO=grpcs` (TLS/HTTP-2). For such a backend the system SHALL contact the cluster over HTTP/2 exactly
(no version downgrade), so gRPC calls — including trailers — are proxied. `grpc` SHALL use the http scheme and
`grpcs` the https scheme for the backend address.

#### Scenario: gRPC backend uses HTTP/2
- **WHEN** a backend declares `VIRTUAL_PROTO=grpc`
- **THEN** the cluster contacts the backend over HTTP/2 (exact version) using the http scheme

#### Scenario: gRPCs backend uses TLS and HTTP/2
- **WHEN** a backend declares `VIRTUAL_PROTO=grpcs`
- **THEN** the cluster contacts the backend over HTTP/2 (exact version) using the https scheme

#### Scenario: gRPC is a recognized protocol
- **WHEN** `VIRTUAL_PROTO` is `grpc` or `grpcs`
- **THEN** it is accepted as a valid protocol (not reported as unsupported)



### Requirement: Per-host configuration overrides
The system SHALL support structured per-host and global configuration overrides layered onto the generated
routes. An override MAY inject response headers for a host; a `default` override SHALL apply to hosts without a
host-specific override. A host-specific override SHALL take precedence over the `default` one. Overrides SHALL
apply to routes regardless of their source (discovery or static config). Additionally, a static-config route
with the same host and path SHALL replace the discovered route for that host/path.

#### Scenario: Per-host response header is injected
- **WHEN** an override for `app.local` adds a response header
- **THEN** responses for `app.local` carry that header

#### Scenario: Default override applies to other hosts
- **WHEN** a `default` override adds a response header and a host has no specific override
- **THEN** responses for that host carry the default header

#### Scenario: Host-specific override wins over default
- **WHEN** both a `default` and an `app.local` override are configured
- **THEN** `app.local` uses its host-specific headers, not the default set

#### Scenario: Static route replaces a generated route
- **WHEN** a static-config route declares the same host and path as a discovered route
- **THEN** the static route definition is used instead of the discovered one

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
