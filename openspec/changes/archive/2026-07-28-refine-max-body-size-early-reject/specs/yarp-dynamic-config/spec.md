## MODIFIED Requirements

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
