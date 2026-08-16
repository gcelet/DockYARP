## ADDED Requirements

### Requirement: Release changelog
WHEN a `vX.Y.Z` git tag is pushed, the system SHALL generate a changelog from the commits since the previous
release tag, grouped by Conventional Commit type (features, fixes, etc.), and SHALL create or update a GitHub
Release for that tag with the generated changelog as its notes. Commit subjects in this repository are
gitmoji-prefixed (e.g. `:sparkles: feat: …`); the changelog generation SHALL match the Conventional Commit
`type:` token regardless of the leading gitmoji, and SHALL exclude OpenSpec archive commits
(`chore: archive <id> into specs`) from the generated notes.

#### Scenario: Tag push generates a release with changelog notes
- **WHEN** a `vX.Y.Z` tag is pushed
- **THEN** a GitHub Release for that tag is created (or updated) with release notes generated from the commits
  since the previous release tag, grouped into sections by Conventional Commit type (Features / Fixes / …)

#### Scenario: Gitmoji-prefixed commits are correctly categorized
- **WHEN** the commit history since the previous release includes a gitmoji-prefixed commit such as
  `:sparkles: feat: add X`
- **THEN** that commit appears under the Features section of the generated changelog, not as an
  unrecognized/uncategorized entry

#### Scenario: Archive commits are excluded from the changelog
- **WHEN** the commit history since the previous release includes a `chore: archive <id> into specs` commit
- **THEN** that commit does not appear as a changelog entry in the generated release notes
