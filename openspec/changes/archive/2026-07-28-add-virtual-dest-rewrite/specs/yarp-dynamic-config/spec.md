## MODIFIED Requirements

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
