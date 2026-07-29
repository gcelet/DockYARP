## MODIFIED Requirements

### Requirement: Host and path matching
The system SHALL select, for a given request host and path, the matching route by **host specificity first** —
an exact host match takes precedence over a wildcard subdomain match (as in nginx-proxy) — and then, among
routes for the same host, by highest priority and then the longest matching path prefix. An exact host match
SHALL support a bare IPv4 address as the host, matching a request whose `Host` is that address. A wildcard
`*.suffix` SHALL match a subdomain of **any depth** (for example `*.local` matches both `app.local` and
`a.b.local`). A **trailing** wildcard `prefix.*` SHALL match any host beginning with `prefix.` (for example
`app.*` matches `app.local` and `app.example.com`). Precedence SHALL be exact host, then leading wildcard, then
trailing wildcard.

#### Scenario: Exact host preferred over wildcard
- **WHEN** routes exist for host `app.local` and for wildcard `*.local`, and a request targets `app.local`
- **THEN** the route for `app.local` is selected

#### Scenario: Exact host wins over a higher-priority wildcard
- **WHEN** an exact-host route and a wildcard route both match a request and the wildcard route has the higher
  priority
- **THEN** the exact-host route is still selected, because host specificity precedes priority (priority only
  orders routes for the same host)

#### Scenario: Wildcard matches a nested subdomain
- **WHEN** a route for wildcard `*.local` exists and a request targets a nested subdomain `a.b.local` with no
  exact route
- **THEN** the wildcard route is selected

#### Scenario: Trailing wildcard matches any suffix
- **WHEN** a route for trailing wildcard `app.*` exists and a request targets `app.example.com` with no exact or
  leading-wildcard route
- **THEN** the trailing-wildcard route is selected

#### Scenario: Leading wildcard preferred over trailing wildcard
- **WHEN** both a leading-wildcard route and a trailing-wildcard route match a request
- **THEN** the leading-wildcard route is selected

#### Scenario: Longest path prefix wins
- **WHEN** two routes match host `app.local` with path prefixes `/` and `/api`, and a request targets `/api/orders`
- **THEN** the route with path prefix `/api` is selected

#### Scenario: No matching route
- **WHEN** no route matches the request host
- **THEN** matching yields no route and the caller can return a not-found/no-route response

#### Scenario: Raw IPv4 host is matched exactly
- **WHEN** a route's host is a bare IPv4 address and a request targets that address as its `Host`
- **THEN** the route is selected
