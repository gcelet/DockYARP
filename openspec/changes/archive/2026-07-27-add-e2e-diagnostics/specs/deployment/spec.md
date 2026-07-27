## ADDED Requirements

### Requirement: End-to-end diagnostics capture
The end-to-end suite SHALL capture each Aspire resource's logs to durable per-resource files under an
artifacts directory during the run, so a failure can be diagnosed after the containers are torn down. Capture
SHALL write to files (not the test console), and the `E2E` build target SHALL surface the diagnostics
directory when the run fails.

#### Scenario: Resource logs persist after teardown
- **WHEN** the end-to-end run finishes or fails and the containers are disposed
- **THEN** each resource's logs remain available in a per-resource file under the artifacts log directory

#### Scenario: Failure surfaces the diagnostics location
- **WHEN** the `E2E` target's test run fails
- **THEN** the build output reports the diagnostics log directory
