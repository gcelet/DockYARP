## ADDED Requirements

### Requirement: nginx-proxy migration guide
The documentation site SHALL provide a standalone page guiding an nginx-proxy operator through migrating to
DockYarp, covering both a basic (public ACME, label-based) setup and an advanced setup using the classic
`nginx-proxy`/`docker-gen`/`acme-companion` trio with environment-variable-based backend configuration and a
private ACME certificate authority. The guide SHALL state that migration only requires replacing the
nginx-proxy stack itself — no other backend service's configuration needs to change. The guide SHALL instruct
copying (not moving) existing certificate files into DockYarp's certificate directory, stating that no format
conversion is required, so the original nginx-proxy installation remains intact and available as a rollback.

#### Scenario: Basic migration path is covered
- **WHEN** an operator running nginx-proxy with `acme-companion` and public ACME reads the guide
- **THEN** they find a direct compose/label translation to the DockYarp equivalent

#### Scenario: Advanced migration path is covered
- **WHEN** an operator running the `nginx-proxy`/`docker-gen`/`acme-companion` trio with environment-variable
  backend configuration and a private ACME certificate authority reads the guide
- **THEN** they find guidance covering that exact pattern, including trusting the private certificate authority
  for DockYarp's own ACME requests

#### Scenario: Backend stacks are not touched
- **WHEN** an operator follows the migration guide
- **THEN** it states that only the nginx-proxy (front-door) stack is replaced — no other backend stack's
  configuration changes

#### Scenario: Certificates are preserved for rollback
- **WHEN** an operator migrates existing certificates
- **THEN** the guide instructs copying (not moving) the certificate files, states no conversion is needed, and
  notes that the original nginx-proxy installation remains available to roll back to
