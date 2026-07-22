## ADDED Requirements

### Requirement: VIRTUAL_DEST path rewrite
The system SHALL read `VIRTUAL_DEST` from a container's labels and configure the route's path rewrite so
the backend receives the rewritten path. When `VIRTUAL_DEST` is absent, no rewrite is configured.

#### Scenario: VIRTUAL_DEST strips the path prefix
- **WHEN** a container declares `VIRTUAL_PATH=/api` and `VIRTUAL_DEST=/`
- **THEN** the mapped route strips `/api` before forwarding, so `/api/x` reaches the backend as `/x`

#### Scenario: No VIRTUAL_DEST keeps the path
- **WHEN** a container declares `VIRTUAL_PATH=/api` and no `VIRTUAL_DEST`
- **THEN** the mapped route forwards the original path unchanged
