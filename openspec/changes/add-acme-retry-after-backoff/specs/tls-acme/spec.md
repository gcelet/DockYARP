## ADDED Requirements

### Requirement: ACME Retry-After-aware backoff
The system SHALL honor a CA-supplied `Retry-After` header (RFC 7231 §7.1.3, either form) where the ACME
protocol allows one: on a `rateLimited` error response (RFC 8555 §6.6), retrying the request once after
waiting the indicated duration (capped — see below) instead of failing immediately; and while polling an
authorization's or order's status, using the indicated duration as the next poll delay instead of a fixed
interval. A `Retry-After` duration used for either purpose SHALL be capped at a bounded maximum so a CA-
supplied value cannot stall a provisioning attempt indefinitely. An error response of a type other than
`rateLimited` SHALL continue to fail immediately regardless of whether it carries a `Retry-After` header —
retrying would not resolve those. Absence of `Retry-After` SHALL leave existing behavior unchanged: a
`rateLimited` error without one still fails immediately (no new retry is invented without a CA-supplied
duration to honor), and polling without one still uses the existing fixed interval.

#### Scenario: A rate-limited request is retried after the indicated wait
- **WHEN** an ACME request receives a `rateLimited` error response carrying a `Retry-After` header
- **THEN** the system waits at least that duration (capped) and retries the request once, rather than failing
  immediately

#### Scenario: A rate-limited request without Retry-After still fails immediately
- **WHEN** an ACME request receives a `rateLimited` error response with no `Retry-After` header
- **THEN** the request fails immediately, unchanged from today — no retry is invented without a duration to
  honor

#### Scenario: A non-rate-limit error is not retried even with Retry-After present
- **WHEN** an ACME request receives an error response of a type other than `rateLimited`, whether or not it
  carries a `Retry-After` header
- **THEN** the request fails immediately, the same as today

#### Scenario: Status polling honors a CA-suggested interval
- **WHEN** DockYarp polls an authorization's or order's status and the response carries a `Retry-After` header
- **THEN** the next poll waits that duration (capped) instead of the fixed default interval

#### Scenario: Status polling falls back to the fixed interval without Retry-After
- **WHEN** DockYarp polls an authorization's or order's status and the response carries no `Retry-After` header
- **THEN** the next poll waits the existing fixed default interval, unchanged from today

#### Scenario: An excessive Retry-After value is capped
- **WHEN** a CA response carries a `Retry-After` value larger than the configured maximum
- **THEN** the system waits only the capped maximum, not the full CA-supplied duration
