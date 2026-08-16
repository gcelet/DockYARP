## MODIFIED Requirements

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
