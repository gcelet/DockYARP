## ADDED Requirements

### Requirement: Container discovery filters
The system SHALL support restricting the set of discovered containers using Docker-native inclusion filters,
configured via `Docker:ContainerFilters` as a map of Docker filter key (e.g. `label`, `name`, `network`) to
one or more values. Values within one key SHALL be OR-combined and distinct keys SHALL be AND-combined (Docker
filter semantics). The filter SHALL be applied to the authoritative container listing used by every
reconciliation pass, so only containers matching the configured filters are considered for routing. When no
filter is configured, discovery SHALL consider all running containers, unchanged from prior behavior.

#### Scenario: Label filter restricts the discovered set
- **WHEN** `Docker:ContainerFilters` restricts discovery to `label=dockyarp.enable=true`
- **THEN** the container listing request carries that Docker filter and only containers with that label are
  considered for routing

#### Scenario: Filtered-out container yields no routing change
- **WHEN** a container that does not match the configured filters starts or stops
- **THEN** reconciliation against the filtered listing produces no route or cluster change for that container

#### Scenario: No filter preserves discovery
- **WHEN** no `Docker:ContainerFilters` is configured
- **THEN** discovery considers all running containers, unchanged from prior behavior
