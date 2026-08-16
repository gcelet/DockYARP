## ADDED Requirements

### Requirement: Structured issue intake
The repository SHALL provide structured GitHub issue templates for inbound bug reports, feature requests, and
questions, and SHALL disable creating a blank (unstructured) issue. `openspec/backlog/` SHALL remain the single
curated source of truth for accepted, spec-ready work: an accepted inbound issue is shaped into a
`openspec/backlog/items/<id>.md` stub that references the originating issue, rather than the repository
maintaining a second, competing backlog inside GitHub Issues.

#### Scenario: A new issue picks a structured template
- **WHEN** someone opens a new issue on the repository
- **THEN** they are offered a structured template (bug report, feature request, or question) and cannot create
  a blank issue

#### Scenario: An accepted issue becomes a backlog stub
- **WHEN** an inbound issue is accepted as work to do
- **THEN** it is shaped into a `openspec/backlog/items/<id>.md` stub referencing the issue, and
  `openspec/backlog/` remains the single source of truth for the work queue
