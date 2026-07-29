## MODIFIED Requirements

### Requirement: Network address selection
The system SHALL select the container address to forward to from the container's Docker networks: when a
preferred network is configured and the container is attached to it, that network's IP SHALL be used;
otherwise the system SHALL choose deterministically among the container's networks and SHALL skip the Swarm
`ingress` network. When the proxy's own networks are configured (`Docker:ProxyNetworks`), the deterministic
choice SHALL be restricted to networks the proxy shares (reachable), and a container reachable on no shared
network SHALL be skipped with a warning rather than routed to an unreachable address. When the proxy's networks
are not configured, the system SHALL fall back to the container name when no network address is available.

#### Scenario: Preferred network is used
- **WHEN** a container is attached to `frontend` (10.0.1.2) and `backend` (10.0.2.2) and the preferred network is `backend`
- **THEN** the forwarded address is `10.0.2.2`

#### Scenario: Swarm ingress network is skipped
- **WHEN** a container is attached to `ingress` (10.0.0.5) and `app` (10.0.1.5) and no preferred network is configured
- **THEN** the forwarded address is `10.0.1.5`

#### Scenario: Selection is deterministic
- **WHEN** a container is attached to several networks with no preferred network configured
- **THEN** the same network's IP is chosen on every reconciliation (ordinal by network name)

#### Scenario: Shared reachable network is selected across multiple networks
- **WHEN** a container is attached to several networks, no preferred network is configured, and
  `Docker:ProxyNetworks` lists one of those networks
- **THEN** the forwarded address is the container's IP on that shared, reachable network

#### Scenario: Backend on no reachable network is skipped
- **WHEN** a container is attached only to networks absent from `Docker:ProxyNetworks`
- **THEN** it has no forwarded address and is skipped with a warning (no broken route or endpoint)
