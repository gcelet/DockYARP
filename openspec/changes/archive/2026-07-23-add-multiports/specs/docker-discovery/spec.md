## ADDED Requirements

### Requirement: Multiple host/path/port mappings per container
The system SHALL read `VIRTUAL_HOST_MULTIPORTS` (a YAML mapping of host → path → `{ port, proto, dest }`) and
map each entry to a route matching that host and path and a cluster targeting the container on the entry's
port and scheme (default `http`). Container-level attributes (auth, load balancing, priority, timeout, body
size, client-certificate requirement, and TLS when the host is a `LETSENCRYPT_HOST`) SHALL apply to the
generated routes. Invalid YAML SHALL be ignored with a warning. A container without the label keeps the
classic `VIRTUAL_HOST` mapping.

#### Scenario: Multiple ports on one host
- **WHEN** a container declares `VIRTUAL_HOST_MULTIPORTS` mapping `app.local` `/` → port 8080 and `/api` → port 9000
- **THEN** two routes are created for `app.local` and `app.local/api`, the latter targeting the container on port 9000

#### Scenario: Multiple hosts
- **WHEN** a container declares multiports entries for `a.local` and `b.local`
- **THEN** routes are created for both hosts

#### Scenario: Invalid YAML is ignored
- **WHEN** a container declares a `VIRTUAL_HOST_MULTIPORTS` value that is not valid YAML
- **THEN** no multiports routes are created for it and the problem is reported
