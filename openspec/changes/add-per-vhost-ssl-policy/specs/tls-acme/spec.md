## ADDED Requirements

### Requirement: Per-host TLS policy (SSL_POLICY)
The system SHALL recognize a per-container `SSL_POLICY` value — set as an environment variable or a label, with
the environment variable taking precedence — that names a TLS preset. During the SNI handshake for a host that
declares a recognized preset, the system SHALL negotiate with that preset's minimum TLS version and cipher
policy, overriding the global TLS posture; a host that declares no `SSL_POLICY` SHALL use the global posture. An
unrecognized per-host `SSL_POLICY` SHALL be ignored (the global posture applies) with a one-time diagnostic.
Per-host `SSL_POLICY` is honored for hosts that are TLS-configured (declare `LETSENCRYPT_HOST` or `CERT_NAME`),
consistent with the other per-host TLS attributes, and SHALL NOT create an ACME certificate desire.

#### Scenario: Per-host preset narrows negotiation
- **WHEN** a certified host declares `SSL_POLICY=Mozilla-Modern`
- **THEN** its handshake enables only TLS 1.3, while a host without the override keeps the global posture

#### Scenario: Unknown per-host policy falls back to global
- **WHEN** a host declares an unrecognized `SSL_POLICY`
- **THEN** the global posture applies and a one-time diagnostic is emitted

#### Scenario: Environment value wins over a same-named label
- **WHEN** a container sets `SSL_POLICY` as both an environment variable and a label
- **THEN** the environment variable's preset is used
