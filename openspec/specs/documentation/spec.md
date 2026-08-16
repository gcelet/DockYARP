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
(`VIRTUAL_HOST`, `VIRTUAL_PORT`, `LETSENCRYPT_HOST`, `DOCKYARP_*`) and the real published image name
(`gcelet/dockyarp`, with `dockyarp:local` documented as the local-build alternative). The Getting Started
quick-start and the Deployment page's Docker Compose example SHALL each work as written against the real
(non-root) runtime image, with no direct Docker socket bind mount and correct host-to-container port mapping.

#### Scenario: Site builds from the repository content
- **WHEN** the site is built with Hugo Extended (Docsy resolved, dependencies installed)
- **THEN** a complete static site is generated from `docs-site/` with a working navigation and section hierarchy

#### Scenario: Both themes are supported
- **WHEN** the site is viewed in light or in dark mode
- **THEN** it renders with the DockYARP palette and readable contrast in either theme

#### Scenario: Content uses real labels
- **WHEN** a reader follows the Getting Started / Configuration content
- **THEN** the examples use `VIRTUAL_HOST`/`VIRTUAL_PORT`/`LETSENCRYPT_HOST`/`DOCKYARP_*`, not invented labels

#### Scenario: The Getting Started quick-start works unmodified
- **WHEN** a new reader follows the Getting Started quick-start verbatim on a standard Docker install
- **THEN** it succeeds against the real non-root runtime image — no direct Docker socket bind mount that a
  non-root, chiseled container cannot open, and host ports mapped to the image's real listen ports (8080/8443)

#### Scenario: The Deployment page's Docker Compose example works unmodified
- **WHEN** a reader follows the Deployment page's Docker Compose example verbatim
- **THEN** it succeeds against the real non-root runtime image — socket access via a socket proxy, and host
  ports 80/443 mapped to the image's real listen ports (8080/8443)

#### Scenario: The published image name is used consistently
- **WHEN** a reader looks at any DockYARP image reference across the site (Getting Started, Examples, Deployment)
- **THEN** it consistently shows `gcelet/dockyarp` as the published image, with `dockyarp:local` documented as
  the local-build alternative

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
built on a base stack shown once. The base stack SHALL state that the `tecnativa/docker-socket-proxy` pattern is
required for the non-root image to reach the Docker API at all, not an optional hardening choice.

#### Scenario: Recipes use real keys and show the expected result
- **WHEN** a reader opens the examples page for a scenario
- **THEN** a copy-pasteable snippet using real labels/environment variables is shown, with the expected result

#### Scenario: The base stack is shown once
- **WHEN** a reader follows the recipes
- **THEN** the `dockyarp` + socket-proxy base stack is presented once and reused by the individual recipes

#### Scenario: The socket-proxy is stated as required
- **WHEN** a reader reads the base stack's introduction
- **THEN** it states plainly that the socket-proxy is required — the non-root image cannot open the Docker
  socket directly — not phrased as optional hardening

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

### Requirement: nginx-proxy migration guide
The documentation site SHALL provide a standalone page guiding an nginx-proxy operator through migrating to
DockYarp, covering both a basic (single-container `nginx-proxy` + `acme-companion`, public ACME) setup and an
advanced setup using the classic separate `nginx`/`docker-gen`/`acme-companion` trio with environment-variable
backend configuration and a private ACME certificate authority. The guide SHALL state that migration only
requires replacing the nginx-proxy stack itself — no other backend service's configuration needs to change. The
guide SHALL instruct copying (not moving) existing certificate files into DockYarp's certificate directory,
stating that no format conversion is required, so the original nginx-proxy installation remains intact and
available as a rollback.

#### Scenario: Basic migration path is covered
- **WHEN** an operator running nginx-proxy with `acme-companion` and public ACME reads the guide
- **THEN** they find a direct compose/label translation to the DockYarp equivalent

#### Scenario: Advanced migration path is covered
- **WHEN** an operator running the `nginx-proxy`/`docker-gen`/`acme-companion` trio with environment-variable
  backend configuration and a private ACME certificate authority reads the guide
- **THEN** they find guidance covering that exact pattern, including trusting the private certificate authority
  for DockYarp's own ACME requests

#### Scenario: Backend stacks are not touched
- **WHEN** an operator follows the migration guide
- **THEN** it states that only the nginx-proxy (front-door) stack is replaced — no other backend stack's
  configuration changes

#### Scenario: Certificates are preserved for rollback
- **WHEN** an operator migrates existing certificates
- **THEN** the guide instructs copying (not moving) the certificate files, states no conversion is needed, and
  notes that the original nginx-proxy installation remains available to roll back to
