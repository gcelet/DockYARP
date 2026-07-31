## ADDED Requirements

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
