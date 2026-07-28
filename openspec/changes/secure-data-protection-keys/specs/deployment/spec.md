## ADDED Requirements

### Requirement: At-rest encryption of Data Protection keys is optional and operator-controlled
DockYarp SHALL encrypt its persisted Data Protection key ring at rest when an operator supplies an encryption
certificate, and SHALL NOT require one when no feature depends on Data Protection. When no encryption certificate
is configured, DockYarp SHALL start normally and SHALL NOT emit the "keys may be persisted unencrypted" warning,
because no sensitive payload is protected. When a configured encryption certificate cannot be loaded, startup
SHALL fail with an actionable error rather than silently falling back to unencrypted keys.

#### Scenario: Key ring encrypted when a certificate is configured
- **WHEN** DockYarp starts with a Data Protection encryption certificate configured
- **THEN** the persisted key ring is protected with that certificate (encrypted at rest)

#### Scenario: No certificate required by default
- **WHEN** DockYarp starts with no Data Protection encryption certificate configured
- **THEN** it starts without requiring one and does not emit the unencrypted-keys warning

#### Scenario: Misconfigured certificate fails fast
- **WHEN** a Data Protection encryption certificate is configured but cannot be loaded (missing file or wrong
  password)
- **THEN** startup fails with an actionable error instead of persisting keys unencrypted
