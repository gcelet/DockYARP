## ADDED Requirements

### Requirement: htpasswd files are reloaded without a restart
The system SHALL periodically reload the htpasswd files from the configured directory so that added, edited, or
removed files take effect without restarting DockYarp. The reload interval SHALL be configurable. A file that
cannot be read during a reload (for example, mid-write) SHALL be skipped for that cycle without failing the
reload.

#### Scenario: Edited htpasswd file takes effect
- **WHEN** an htpasswd file's credentials change while DockYarp is running
- **THEN** subsequent requests are validated against the updated credentials within the reload interval

#### Scenario: Removed htpasswd file drops protection
- **WHEN** an htpasswd file is removed while DockYarp is running
- **THEN** that route's htpasswd protection is dropped within the reload interval
