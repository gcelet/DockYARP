## ADDED Requirements

### Requirement: Configuration change notification
The route configuration store SHALL notify observers when it publishes a new snapshot, and SHALL NOT
notify them when an update is a no-op (identical content). The notification enables consumers such as the
YARP integration to reload without polling.

#### Scenario: Observers notified on content change
- **WHEN** an update changes the published routes or clusters
- **THEN** registered observers are notified after the new snapshot becomes current

#### Scenario: No notification on a no-op update
- **WHEN** an update is applied whose content is identical to the current snapshot
- **THEN** no change notification is raised
