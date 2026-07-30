## ADDED Requirements

### Requirement: Client-side documentation search
The documentation site SHALL provide client-side search over its content. By default it SHALL use an offline
Lunr index generated at build time, with the Lunr library self-hosted so no request is made to a CDN or search
service. Algolia DocSearch SHALL be supported as an opt-in alternative via configuration; when it is not
configured, the site SHALL fall back to the offline Lunr search.

#### Scenario: Offline search returns results without an external service
- **WHEN** a user types a query in the search box and Algolia is not configured
- **THEN** matching pages are returned client-side from the local Lunr index, with no request to a CDN or
  search service

#### Scenario: Algolia is used when configured
- **WHEN** `params.search.algolia` is configured
- **THEN** the site uses Algolia DocSearch instead of the offline Lunr search
