# documentation Specification

## Purpose
The DockYARP documentation site — a Hugo + Docsy static site under `docs-site/` — and the requirements
governing its structure, theming, and content.

## Requirements
### Requirement: Documentation site scaffold
The project SHALL provide a Hugo + Docsy documentation site under `docs-site/`, isolated from the .NET build,
carrying the DockYARP brand identity. The site SHALL define an information architecture with navigable sections
(at least Getting Started, Configuration, Architecture, Deployment, Contributing) and SHALL render correctly in
both light and dark themes. Documentation content SHALL use DockYARP's real container labels
(`VIRTUAL_HOST`, `VIRTUAL_PORT`, `LETSENCRYPT_HOST`, `DOCKYARP_*`).

#### Scenario: Site builds from the repository content
- **WHEN** the site is built with Hugo Extended (Docsy resolved, dependencies installed)
- **THEN** a complete static site is generated from `docs-site/` with a working navigation and section hierarchy

#### Scenario: Both themes are supported
- **WHEN** the site is viewed in light or in dark mode
- **THEN** it renders with the DockYARP palette and readable contrast in either theme

#### Scenario: Content uses real labels
- **WHEN** a reader follows the Getting Started / Configuration content
- **THEN** the examples use `VIRTUAL_HOST`/`VIRTUAL_PORT`/`LETSENCRYPT_HOST`/`DOCKYARP_*`, not invented labels

### Requirement: Self-hosted fonts and accessible theming
The documentation site SHALL self-host its web fonts (no external font CDN request) and SHALL meet WCAG AA
contrast for body text, muted text, links, and code in both light and dark themes. The bundled fonts SHALL
carry their open-source license.

#### Scenario: Fonts load from the origin
- **WHEN** a page is loaded
- **THEN** its fonts are served from the site's own `/fonts/` path and no request is made to a font CDN

#### Scenario: Contrast meets WCAG AA in both themes
- **WHEN** the site is viewed in light or dark mode
- **THEN** body/muted/link/code foreground–background pairs meet WCAG AA contrast

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

### Requirement: Container configuration reference (labels and environment variables)
The documentation site SHALL provide a configuration reference that documents every container label and
environment variable DockYARP recognizes — the nginx-proxy-compatible keys (`VIRTUAL_*`, `LETSENCRYPT_*`,
`CERT_NAME`, `SSL_POLICY`, `HTTPS_METHOD`, `HSTS`, `NETWORK_ACCESS`, `SERVER_TOKENS`, `EXTERNAL_HTTPS_PORT`,
`ENABLE_HTTP_ON_MISSING_CERT`, `TRUST_DEFAULT_CERT`) and the `DOCKYARP_*` keys — with each entry's behavior,
default, and a realistic example using real key names. The reference SHALL state that any key may be set as a
label or as an environment variable, the environment variable taking precedence when both are set, and SHALL
note the recognized nginx-proxy namespaced label aliases.

#### Scenario: Every recognized key is documented
- **WHEN** a reader opens the configuration reference
- **THEN** every container label / environment variable DockYARP reads is listed with its behavior, default, and
  a realistic example (real key names, no placeholders)

#### Scenario: The label-or-environment channel is explained
- **WHEN** a reader consults the reference
- **THEN** it states that any key may be set as a label or an environment variable, with the environment
  variable winning when both are set for the same key

### Requirement: Application configuration reference
The documentation site SHALL document the proxy's own application configuration — each configuration section
(`Server`, `Docker`, `Tls`, `Security`, `Routing`, `Proxy`, `AccessLog`, `AdminApi`, `Compression`,
`DataProtection`, `Host`) with its keys, their defaults, and their purpose — and SHALL state that any key may be
set via `appsettings.json` or a double-underscore environment variable (for example `Tls__AcceptTermsOfService`).

#### Scenario: Each application-configuration section is documented with defaults
- **WHEN** a reader opens the Configuration page's application-configuration reference
- **THEN** every section is listed with its keys, each key's default, and its purpose

#### Scenario: The appsettings-or-environment channel is explained
- **WHEN** a reader consults the application-configuration reference
- **THEN** it states that any key may be set via `appsettings.json` or a `Section__Key` environment variable

### Requirement: Runtime feature reference
The documentation site SHALL document DockYARP's runtime features — Docker discovery (health-aware exclusion,
network selection, container filters), routing and load balancing (including regex path matching), automatic
TLS/ACME behavior, access control (Basic Auth via labels or htpasswd files, and internal-only routes), response
compression and httpoxy `Proxy`-header stripping, observability (the `/metrics` Prometheus endpoint and the
structured access log with its field catalog), the admin API endpoints (`/api/routes`, `/api/clusters`,
`/api/certs`, `/api/health`, `/api/resolve`, protected by the API key), file-based static configuration including
per-host response-header overrides, custom error pages, and graceful shutdown — describing each feature's
behavior with a realistic example where applicable.

#### Scenario: Runtime features are documented
- **WHEN** a reader opens the runtime features reference
- **THEN** discovery, routing/load balancing, TLS, access control (including htpasswd files), response
  compression, observability, the admin API, static configuration (including per-host overrides), error pages,
  and graceful shutdown are each described with their behavior

#### Scenario: The admin API endpoints are documented
- **WHEN** a reader consults the runtime features reference
- **THEN** the read-only endpoints (`/api/routes`, `/api/clusters`, `/api/certs`, `/api/health`) and
  `/api/resolve` are listed with the `X-Api-Key` requirement

### Requirement: Worked configuration examples
The documentation site SHALL provide worked, copy-pasteable configuration recipes for common scenarios — at
least a basic virtual host, path routing with a rewrite, multiple ports, configuration via environment
variables, automatic HTTPS, mutual TLS, a per-host TLS policy, Basic Auth, internal-only access, and running
behind a load balancer — each using real DockYARP labels/environment variables and stating the expected result,
built on a base stack shown once.

#### Scenario: Recipes use real keys and show the expected result
- **WHEN** a reader opens the examples page for a scenario
- **THEN** a copy-pasteable snippet using real labels/environment variables is shown, with the expected result

#### Scenario: The base stack is shown once
- **WHEN** a reader follows the recipes
- **THEN** the `dockyarp` + socket-proxy base stack is presented once and reused by the individual recipes

### Requirement: Continuous documentation build and publish
The documentation site SHALL be built **reproducibly** — with a pinned Hugo Extended toolchain and no ambient
dependency — via a dedicated build target, and SHALL be published to a static host (GitHub Pages by default) by CI:
a build check on every pull request, and a build-and-publish on every push to the default development branch. The
documentation build SHALL be isolated from the application build so the two do not cross-contaminate.

#### Scenario: Reproducible local/CI build
- **WHEN** the documentation build target runs
- **THEN** it installs the pinned Hugo Extended toolchain and produces a complete static site, with no reliance on an
  ambiently-installed Hugo

#### Scenario: Published on push to the development branch
- **WHEN** a commit that changes the documentation is pushed to the default development branch
- **THEN** CI builds the site and publishes it to GitHub Pages

#### Scenario: Build check on a pull request
- **WHEN** a pull request changes the documentation
- **THEN** CI builds the site (without publishing) and fails the check if the build fails

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

### Requirement: Release process reference
The documentation site SHALL provide a standalone page walking a contributor through cutting a release: the
one-time bootstrap step of creating the `main` branch for the first release (merging `develop` in and tagging
`v0.1.0`); how to read the version GitVersion would compute before tagging; the exact command to push a release
tag; and a summary of what happens automatically afterward (changelog generation and GitHub Release creation,
tagged image publish), linking to the authoritative workflow files rather than duplicating their behavior. The
release process SHALL be documented in exactly one place on the site.

#### Scenario: Contributor finds the release steps in one place
- **WHEN** a contributor opens the Releasing page
- **THEN** they find, in order, the version-check step, the tag command, and a summary of what runs
  automatically after the tag is pushed — without needing to read `GitVersion.yml` or the workflow YAML directly

#### Scenario: First-release bootstrap is covered
- **WHEN** a contributor reads the Releasing page before any release has been cut
- **THEN** the page explicitly describes the one-time step of creating `main` from `develop` and tagging `v0.1.0`

#### Scenario: Release process is not duplicated elsewhere
- **WHEN** the Contributing page mentions releases
- **THEN** it points to the Releasing page rather than restating the steps
