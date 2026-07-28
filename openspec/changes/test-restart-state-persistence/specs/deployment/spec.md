## ADDED Requirements

### Requirement: End-to-end restart-persistence assertion
The end-to-end suite SHALL assert, against the real runtime, that persisted state survives a container
recreation: after a certificate is provisioned for a TLS host, restarting the DockYarp container against the
same certificate volume SHALL serve the same certificate (reused from the volume) rather than re-provisioning a
new one. This assertion SHALL be integrated into the existing end-to-end suite (runnable on demand and in release
validation, excluded from the default build).

#### Scenario: Provisioned certificate is reused after a container restart
- **WHEN** DockYarp has provisioned a certificate for a TLS host and its container is restarted against the same
  certificate volume
- **THEN** after the container is healthy again it serves the same certificate (same thumbprint) for that host,
  proving the persisted state survived the recreation
