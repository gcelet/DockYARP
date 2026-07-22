## ADDED Requirements

### Requirement: Network address selection
The system SHALL select the container address to forward to from the container's Docker networks: when a
preferred network is configured and the container is attached to it, that network's IP SHALL be used;
otherwise the system SHALL choose deterministically among the container's networks, SHALL skip the Swarm
`ingress` network, and SHALL fall back to the container name when no network address is available.

#### Scenario: Preferred network is used
- **WHEN** a container is attached to `frontend` (10.0.1.2) and `backend` (10.0.2.2) and the preferred network is `backend`
- **THEN** the forwarded address is `10.0.2.2`

#### Scenario: Swarm ingress network is skipped
- **WHEN** a container is attached to `ingress` (10.0.0.5) and `app` (10.0.1.5) and no preferred network is configured
- **THEN** the forwarded address is `10.0.1.5`

#### Scenario: Selection is deterministic
- **WHEN** a container is attached to several networks with no preferred network configured
- **THEN** the same network's IP is chosen on every reconciliation (ordinal by network name)
