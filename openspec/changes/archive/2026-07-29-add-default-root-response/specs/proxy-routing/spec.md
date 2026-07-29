## ADDED Requirements

### Requirement: Default response for unmatched requests
When a request matches no route and no default host, the system SHALL return a configurable default response:
either a fixed status code (the default) or a redirect to a configured target. A redirect target MAY use the
templates `$scheme`, `$host`, and `$request_uri`, with `$$` denoting a literal `$`. A configured default host
SHALL take precedence over this default response.

#### Scenario: Default status for unmatched request
- **WHEN** no default redirect is configured and a request matches no route or default host
- **THEN** the response is the configured default status code

#### Scenario: Default redirect with substitution
- **WHEN** the default response is configured to redirect to `https://$host$request_uri` and an unmatched request
  arrives for host `app.local` and path `/x?a=1`
- **THEN** the response is a redirect whose `Location` is `https://app.local/x?a=1`

#### Scenario: Default host precedence
- **WHEN** a default host is configured and a request matches no explicit route
- **THEN** the default host serves the request rather than the default response
