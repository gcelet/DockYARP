## ADDED Requirements

### Requirement: ACME certificate revocation
The system SHALL support revoking a stored certificate via the ACME provider (RFC 8555 §7.6), signing the
revocation request with the same persisted account key that issued (or would issue) a certificate for that
host's resolved (contact email, ACME directory endpoint) pair. On a successful revocation, the system SHALL
remove the certificate from the certificate store (its `.crt`/`.key` PEM pair and any legacy `.pfx`) so the
existing provisioning/renewal reconcile loop requests a fresh certificate — with a fresh private key — on its
next pass, rather than continuing to serve the revoked one. Revocation SHALL be exposed only through the admin
dashboard, gated by its own explicit opt-in (`AdminApi:AllowCertificateRevocation`), independent of the
existing certificate-conversion opt-in.

#### Scenario: A revoked certificate is removed from the store
- **WHEN** an operator revokes a stored certificate for a host and the ACME provider confirms the revocation
- **THEN** the certificate store no longer has a certificate for that host

#### Scenario: A removed certificate is re-provisioned on the next reconcile pass
- **WHEN** a host's certificate has been removed following a revocation, and the host still declares TLS
  metadata
- **THEN** the next provisioning/renewal pass requests and stores a new certificate — with a new private
  key — for that host, the same as if no certificate had ever existed

#### Scenario: Revocation is not available unless explicitly enabled
- **WHEN** `AdminApi:AllowCertificateRevocation` is not set (or `false`)
- **THEN** the dashboard does not expose a revoke action, and a direct request to the revoke route has no
  effect

#### Scenario: Revocation is gated independently of certificate conversion
- **WHEN** `AdminApi:AllowCertificateConversion` is enabled but `AdminApi:AllowCertificateRevocation` is not
- **THEN** the "Convert to PEM"/"Re-encrypt key" actions remain available but the revoke action does not
