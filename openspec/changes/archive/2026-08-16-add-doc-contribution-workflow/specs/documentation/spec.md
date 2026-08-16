## MODIFIED Requirements

### Requirement: Contributing and development guidance
The documentation site SHALL provide contributor guidance covering: the environment setup needed before any of
the rest applies (required tooling, the OpenSpec CLI and its Node dependency, and Docker scoped to only what
needs it); the spec-driven change lifecycle; **how a contributor without direct push access submits a change**
(fork the repository, branch off the trunk, run the same lifecycle locally, open a pull request against the
trunk, following the commit convention documented in `AGENTS.md`); the current branch model (which branch is
the trunk, and that the release branch is reserved for releases); the build and test commands; the
**test-pyramid strategy** (unit / integration / end-to-end, and when each applies) with a link to the
repository's e2e coverage map; and pointers to the authoritative in-repo developer docs (testing, architecture,
conventions) rather than duplicating them. Environment setup guidance SHALL distinguish tooling required to
contribute at all from tooling specific to Claude Code (called out as optional, layered on top of the OpenSpec
CLI rather than a separate requirement). Links to repository files SHALL derive from the centralized target
branch, so they do not break when the branch changes.

#### Scenario: Contributor finds the test strategy
- **WHEN** a contributor opens the Contributing page
- **THEN** they find the test-pyramid strategy and a link to the repository's testing / coverage document

#### Scenario: Repo-doc links follow the configured branch
- **WHEN** the Contributing page links to an in-repo developer doc
- **THEN** the link targets the configured branch (a single setting), not a hardcoded branch

#### Scenario: A new contributor finds what to install
- **WHEN** a contributor with a clean machine opens the Contributing page
- **THEN** they find the required tooling — including the OpenSpec CLI and the Node dependency it needs — listed
  before the change-lifecycle content, with Docker called out as required only for the end-to-end suite

#### Scenario: Claude Code tooling is presented as optional
- **WHEN** a contributor not using Claude Code reads the environment setup guidance
- **THEN** the Claude-Code-specific tooling (MCP servers, slash commands) is clearly marked optional, separate
  from the OpenSpec CLI requirement that applies to every contributor

#### Scenario: A contributor without push access finds how to submit a change
- **WHEN** a contributor who does not have direct push access reads the Contributing page
- **THEN** they find that they fork the repository, branch off the trunk, run the same change lifecycle
  locally, and open a pull request against the trunk — with the commit format pointing to `AGENTS.md` rather
  than being restated

#### Scenario: The branch model is stated
- **WHEN** a contributor wonders which branch to base their work on
- **THEN** the page states which branch is the current trunk and that the release branch is reserved for
  releases
