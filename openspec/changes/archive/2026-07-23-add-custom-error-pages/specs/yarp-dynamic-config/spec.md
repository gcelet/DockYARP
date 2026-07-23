## ADDED Requirements

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
