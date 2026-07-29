## ADDED Requirements

### Requirement: File-based Basic Auth (htpasswd)
The system SHALL support Basic Auth credentials loaded from mounted Apache htpasswd files, in addition to label
credentials. A file named for a host SHALL protect that host, and a file named for a host and a path SHALL
protect only that path. The system SHALL verify credentials against bcrypt, Apache apr1, and SHA1 (`{SHA}`)
password hashes, and SHALL reject unrecognized hash formats. A request SHALL be authorized when it matches either
a label credential or any htpasswd entry for the route; a route with no credential from either source SHALL
remain open. Credentials SHALL NOT be logged.

#### Scenario: htpasswd protects a host
- **WHEN** an htpasswd file exists for a host and a request arrives without valid credentials
- **THEN** the response is 401 with a `WWW-Authenticate: Basic` challenge, and a request presenting a valid
  htpasswd user is allowed

#### Scenario: Multiple htpasswd users
- **WHEN** an htpasswd file lists several users
- **THEN** any of those users' valid credentials authorize the request

#### Scenario: Path-scoped htpasswd
- **WHEN** an htpasswd file is scoped to a specific path
- **THEN** only requests to that path require those credentials

#### Scenario: Supported hash formats
- **WHEN** an htpasswd entry uses a bcrypt, apr1, or SHA1 hash and a request presents the matching password
- **THEN** the credential is accepted; an entry with an unrecognized hash format never authorizes a request

#### Scenario: Label and file credentials combined
- **WHEN** a route has both a label credential and an htpasswd file
- **THEN** a request matching either source is authorized
