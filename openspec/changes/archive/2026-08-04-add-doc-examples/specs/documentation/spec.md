## ADDED Requirements

### Requirement: Worked configuration examples
The documentation site SHALL provide worked, copy-pasteable configuration recipes for common scenarios — at
least a basic virtual host, path routing with a rewrite, multiple ports, configuration via environment
variables, automatic HTTPS, mutual TLS, a per-host TLS policy, Basic Auth, internal-only access, and running
behind a load balancer — each using real DockYARP labels/environment variables and stating the expected result,
built on a base stack shown once.

#### Scenario: Recipes use real keys and show the expected result
- **WHEN** a reader opens the examples page for a scenario
- **THEN** a copy-pasteable snippet using real labels/environment variables is shown, with the expected result

#### Scenario: The base stack is shown once
- **WHEN** a reader follows the recipes
- **THEN** the `dockyarp` + socket-proxy base stack is presented once and reused by the individual recipes
