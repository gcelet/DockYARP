## ADDED Requirements

### Requirement: Path rewrite transform
When a route defines a path-remove-prefix transform, the system SHALL strip that prefix from the request
path before forwarding to the backend; routes without a transform SHALL forward the original path.

#### Scenario: Prefix stripped before forwarding
- **WHEN** a route for `app.local` with path prefix `/api` sets a path-remove-prefix of `/api` and a request targets `/api/orders`
- **THEN** the backend receives the request path `/orders`

#### Scenario: No transform forwards the original path
- **WHEN** a route defines no path transform and a request targets `/api/orders`
- **THEN** the backend receives `/api/orders` unchanged
