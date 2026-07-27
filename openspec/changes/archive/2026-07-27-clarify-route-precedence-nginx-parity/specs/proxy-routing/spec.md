## MODIFIED Requirements

### Requirement: Host and path matching
The system SHALL select, for a given request host and path, the matching route by **host specificity first** —
an exact host match takes precedence over a wildcard subdomain match (as in nginx-proxy) — and then, among
routes for the same host, by highest priority and then the longest matching path prefix.

#### Scenario: Exact host preferred over wildcard
- **WHEN** routes exist for host `app.local` and for wildcard `*.local`, and a request targets `app.local`
- **THEN** the route for `app.local` is selected

#### Scenario: Exact host wins over a higher-priority wildcard
- **WHEN** an exact-host route and a wildcard route both match a request and the wildcard route has the higher
  priority
- **THEN** the exact-host route is still selected, because host specificity precedes priority (priority only
  orders routes for the same host)

#### Scenario: Longest path prefix wins
- **WHEN** two routes match host `app.local` with path prefixes `/` and `/api`, and a request targets `/api/orders`
- **THEN** the route with path prefix `/api` is selected

#### Scenario: No matching route
- **WHEN** no route matches the request host
- **THEN** matching yields no route and the caller can return a not-found/no-route response
