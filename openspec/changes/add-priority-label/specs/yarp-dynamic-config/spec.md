## ADDED Requirements

### Requirement: Route priority ordering
The system SHALL map a route's priority to YARP's route order so that a higher priority takes precedence
(YARP treats a lower order as higher precedence); a priority of `0` leaves the route at YARP's default
order.

#### Scenario: Higher priority yields higher precedence
- **WHEN** a route has priority `5`
- **THEN** the mapped YARP route order is `-5` (higher precedence than a priority-`0` route)

#### Scenario: Default priority keeps the default order
- **WHEN** a route has priority `0`
- **THEN** the mapped YARP route leaves the order unset
