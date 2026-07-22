## ADDED Requirements

### Requirement: Route client-certificate requirement
The routing model SHALL allow a route to carry a client-certificate requirement (`none` (default),
`optional`, or `required`) so the security capability can enforce mutual TLS per host.

#### Scenario: Route requires a client certificate
- **WHEN** a route is created with a client-certificate requirement of `required`
- **THEN** the model exposes that requirement for the security layer to enforce

#### Scenario: Default requirement is none
- **WHEN** a route is created without a client-certificate requirement
- **THEN** the model exposes a requirement of `none`
