## ADDED Requirements

### Requirement: Regex path matching
The system SHALL support a `VIRTUAL_PATH` expressed as a `~`-prefixed regular expression, routing a request to
that route when the request path matches the expression. The regex SHALL be compiled with a bounded match
timeout and SHALL fail closed on a timeout or an invalid expression (never routing, never stalling the request).
A prefix path (a more specific match) SHALL take precedence over a regex path. `VIRTUAL_DEST` SHALL NOT apply to
a regex path (a regex location has no fixed prefix to rewrite), and any destination SHALL be ignored in that
case.

#### Scenario: Regex path matches
- **WHEN** a route sets `VIRTUAL_PATH=~^/(app1|alt1)/` and a request targets `/app1/x`
- **THEN** the request is routed to that route; a request to `/other` is not

#### Scenario: Prefix path preferred over regex path
- **WHEN** a prefix-path route and a regex-path route both match a request
- **THEN** the prefix-path route is selected

#### Scenario: VIRTUAL_DEST ignored for a regex path
- **WHEN** a regex `VIRTUAL_PATH` is combined with a `VIRTUAL_DEST`
- **THEN** no path rewrite is applied (the destination is ignored)
