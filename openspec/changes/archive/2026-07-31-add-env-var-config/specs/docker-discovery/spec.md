## MODIFIED Requirements

### Requirement: Label schema and parsing
The system SHALL parse nginx-proxy-compatible configuration (`VIRTUAL_HOST`, `VIRTUAL_PORT`, `VIRTUAL_PATH`,
`LETSENCRYPT_HOST`, `LETSENCRYPT_EMAIL`) and `DOCKYARP_*` settings from a container's **environment variables**
and **labels** into a strongly-typed configuration object, applying documented defaults (e.g. a default target
port when `VIRTUAL_PORT` is absent). When a value is set as both an environment variable and a label, the
**environment variable SHALL take precedence** (environment variables are nginx-proxy's canonical channel; the
label is the fallback).

#### Scenario: Host and port produce a routable target
- **WHEN** a container declares `VIRTUAL_HOST=app.local` and `VIRTUAL_PORT=8080`
- **THEN** the parsed configuration targets that container on port 8080 for host `app.local`

#### Scenario: Default port when VIRTUAL_PORT is absent
- **WHEN** a container declares `VIRTUAL_HOST=app.local` with no `VIRTUAL_PORT` and exposes a single port
- **THEN** the parsed configuration targets that single exposed port

#### Scenario: Configuration is read from environment variables
- **WHEN** a container sets `VIRTUAL_HOST`/`VIRTUAL_PORT` as environment variables (not labels)
- **THEN** it is parsed and routed the same as if they were labels

#### Scenario: Environment variable overrides a same-named label
- **WHEN** a container sets the same key as both an environment variable and a label
- **THEN** the environment variable's value is used
