## ADDED Requirements

### Requirement: Cluster endpoint de-duplication
When mapping a cluster to YARP, the system SHALL de-duplicate destinations by endpoint id (last definition
wins), so duplicate endpoints — for example from a repeated host in `VIRTUAL_HOST` or a repeated static
address — never fail configuration.

#### Scenario: Duplicate endpoints collapse to one destination
- **WHEN** a cluster contains two endpoints with the same id
- **THEN** the mapped YARP cluster has a single destination and mapping does not fail
