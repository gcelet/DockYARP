## ADDED Requirements

### Requirement: Single route resolution per request
The request pipeline SHALL resolve the matched route at most once per request and reuse that result across
the middlewares that need it, rather than resolving it independently in each. The shared result SHALL be
stable for the lifetime of the request.

#### Scenario: The resolved route is cached for the request
- **WHEN** the route has been resolved for a request and the routing store subsequently changes
- **THEN** the same request continues to observe the route resolved on first access
