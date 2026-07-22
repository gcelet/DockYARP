## ADDED Requirements

### Requirement: VIRTUAL_PROTO backend scheme
The system SHALL read `VIRTUAL_PROTO` from a container's labels and use it as the backend scheme (`http`
or `https`), defaulting to `http` when absent and falling back to `http` (with a warning) for unsupported
values.

#### Scenario: HTTPS backend
- **WHEN** a container declares `VIRTUAL_PROTO=https` and `VIRTUAL_PORT=443`
- **THEN** the mapped endpoint targets the container over HTTPS on port 443

#### Scenario: Unsupported value falls back to http
- **WHEN** a container declares `VIRTUAL_PROTO=fastcgi` (not yet supported)
- **THEN** the endpoint targets HTTP and a warning is logged
