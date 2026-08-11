## ADDED Requirements

### Requirement: Contributing and development guidance
The documentation site SHALL provide contributor guidance covering: the spec-driven change lifecycle; the build and
test commands; the **test-pyramid strategy** (unit / integration / end-to-end, and when each applies) with a link to
the repository's e2e coverage map; and pointers to the authoritative in-repo developer docs (testing, architecture,
conventions) rather than duplicating them. Links to repository files SHALL derive from the centralized target branch,
so they do not break when the branch changes.

#### Scenario: Contributor finds the test strategy
- **WHEN** a contributor opens the Contributing page
- **THEN** they find the test-pyramid strategy and a link to the repository's testing / coverage document

#### Scenario: Repo-doc links follow the configured branch
- **WHEN** the Contributing page links to an in-repo developer doc
- **THEN** the link targets the configured branch (a single setting), not a hardcoded branch
