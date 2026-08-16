# deployment Specification

## Purpose
TBD - created by archiving change add-deployment. Update Purpose after archive.
## Requirements
### Requirement: Docker image
The system SHALL be packaged as a minimal multi-stage Docker image whose build stage runs the Nuke build
pipeline and whose runtime stage is a chiseled .NET runtime, running as a non-root user, exposing the proxy
ports and supporting mounted `/certs` and `/config` volumes.

#### Scenario: Container starts with mounted volumes
- **WHEN** the image is started with volumes mounted for certificates and configuration
- **THEN** DockYarp starts and reads certificates and configuration from those volumes

### Requirement: Reference Compose stack
The system SHALL provide a reference `docker-compose.yml` demonstrating label-based configuration for at
least one sample service reachable through DockYarp by its `VIRTUAL_HOST`. DockYarp SHALL run as a
non-root container and, by default, reach the Docker API through a socket-proxy service rather than mounting
the Docker socket directly.

#### Scenario: Sample service reachable through the proxy
- **WHEN** the reference stack is started and a request is sent to DockYarp with the sample service's `VIRTUAL_HOST`
- **THEN** the request is proxied to the sample service and returns its response

#### Scenario: Discovery works without mounting the socket into DockYarp
- **WHEN** the reference stack is started
- **THEN** DockYarp discovers containers through the socket-proxy endpoint while running as a non-root
  container that does not mount the Docker socket

### Requirement: Graceful shutdown
The system SHALL shut down gracefully on a termination signal, draining in-flight requests and stopping
background workers (discovery, provisioning) cleanly within a bounded timeout.

#### Scenario: In-flight requests are drained
- **WHEN** the container receives a stop signal while requests are in flight
- **THEN** in-flight requests are allowed to complete within the shutdown timeout before the process exits

### Requirement: Image publishing
The build pipeline SHALL publish the Docker image to a configurable container registry, defaulting to
Docker Hub, tagged with a configurable tag (default `latest`). The image SHALL be built through the Nuke
pipeline (the Docker build stage runs the build), and publishing SHALL assume the environment is already
authenticated to the registry.

#### Scenario: Publish to the default registry
- **WHEN** the publish target runs without a registry override
- **THEN** the image is built via the pipeline and pushed as `{repository}:{tag}` to Docker Hub

#### Scenario: Publish to a custom registry
- **WHEN** a registry host is provided
- **THEN** the image is pushed as `{registry}/{repository}:{tag}`

### Requirement: Continuous image publishing
On a release (a `v*` tag) or a push to the trunk branch, the system SHALL build and push the application Docker
image via CI to a **configurable** container registry — GitHub Container Registry (GHCR) by default, or **any
other registry, including a private, non-Docker-Hub registry** (for example a self-hosted Harbor, Azure
Container Registry, GitLab, or Nexus) — identified by a configurable registry host and repository,
authenticating with credentials supplied as secrets (defaulting to the GitHub token for GHCR). The published
image SHALL be a **multi-architecture** manifest (at least `linux/amd64` and `linux/arm64`), built from the
repository `Dockerfile` (whose build stage runs the Nuke build). The published **tag set** SHALL depend on the
release channel: a **stable** release (no pre-release suffix) SHALL push the exact version, its `Major.Minor`,
its `Major`, and `latest`; a **prerelease** (a `v*` tag with a pre-release suffix) SHALL push only its exact
version, leaving `latest` and the rolling `Major.Minor`/`Major` tags untouched; a push to the trunk branch (no
tag) SHALL push its GitVersion-resolved prerelease version plus an `edge` tag.

#### Scenario: Publish to the default registry (GHCR) on release
- **WHEN** a stable version tag `vX.Y.Z` is pushed and no custom registry is configured
- **THEN** the image is built and pushed to GHCR as `ghcr.io/{repository}:X.Y.Z`, `:X.Y`, `:X`, and `:latest`

#### Scenario: Prerelease tags do not move the rolling or latest tags
- **WHEN** a prerelease version tag `vX.Y.Z-<pre>` is pushed
- **THEN** the image is pushed only as `{repository}:X.Y.Z-<pre>`, and the `Major.Minor`/`Major`/`latest` tags
  are left pointing at whatever they previously pointed to

#### Scenario: A trunk push publishes the edge channel
- **WHEN** a commit is pushed to the trunk branch (no tag)
- **THEN** the image is pushed as `{repository}:edge` and as the GitVersion-resolved prerelease version for
  that commit (e.g. `{repository}:0.1.0-alpha.223`)

#### Scenario: Publish to a configured private registry
- **WHEN** a custom registry host and credentials are configured (`IMAGE_REGISTRY` + registry username/password
  secrets) and a release is published
- **THEN** the image is pushed as `{registry}/{repository}:{version}` (plus the rolling/latest tags for a
  stable release) on that private registry, authenticated with the supplied credentials

#### Scenario: Multi-architecture image
- **WHEN** the image is published
- **THEN** the pushed manifest includes at least `linux/amd64` and `linux/arm64`

### Requirement: Continuous integration
The project SHALL run its build-and-test gate automatically on every pull request and on every push to the
default branch, via a GitHub Actions workflow that checks out the repository, sets up the .NET SDK pinned by
`global.json`, restores and compiles the whole solution, and runs the Nuke `Test` gate (unit + integration
tests, warnings treated as errors). A build warning or a test failure SHALL fail the check. The end-to-end
suite (which requires a Docker daemon) is out of scope of this gate.

#### Scenario: CI runs on a pull request
- **WHEN** a pull request is opened or updated
- **THEN** the workflow restores, compiles the solution, and runs the Nuke `Test` gate, failing the check on any
  build warning or test failure

#### Scenario: CI runs on push to the default branch
- **WHEN** a commit is pushed to the default branch
- **THEN** the same build-and-test gate runs

### Requirement: Automated dependency updates
The project SHALL keep its dependencies current via Renovate, configured to cover the NuGet Central Package
Management versions (`Directory.Packages.props`), the GitHub Actions workflows, the `Dockerfile` base image
(pinned to a digest and kept updated), and the docs-site npm packages — opening **grouped** update pull requests
that the CI gate validates. The relaxed .NET SDK floor in `global.json` (any installed .NET 10) SHALL NOT be
re-pinned by Renovate.

#### Scenario: Grouped dependency update PRs
- **WHEN** a covered dependency (a NuGet package, a GitHub Action, the base image, or an npm package) has an update
- **THEN** Renovate opens a pull request per the grouping policy, which the CI gate validates

#### Scenario: SDK floor left relaxed
- **WHEN** the .NET SDK has a newer release
- **THEN** Renovate does not re-pin `global.json` (the any-.NET-10 floor is preserved)

### Requirement: Build versioning
The build SHALL derive its version from git (base version + commit height + `v*` tags) using GitVersion — declared
as the local .NET tool `gitversion.tool` in `.config/dotnet-tools.json` and configured by a root `GitVersion.yml` —
computing the version **once on the host** in the Nuke build and stamping the assemblies
(`AssemblyVersion`/`FileVersion`/`InformationalVersion`, the latter including the commit id). The running build's
version SHALL be exposed by the Admin API. Because the Docker build context excludes `.git`, the image SHALL be
stamped with the **same** host-computed version, passed to the build as a build argument, without running GitVersion
inside the container.

#### Scenario: Assemblies stamped from git
- **WHEN** the build runs in a git working tree
- **THEN** the produced assemblies' informational version reflects the GitVersion-derived version (base version +
  git height + commit id)

#### Scenario: Admin API reports the version
- **WHEN** a client calls `GET /api/version` (with a valid API key)
- **THEN** the response reports the running build's version

#### Scenario: Image stamped without .git in the context
- **WHEN** the Docker image is built (its context excludes `.git`)
- **THEN** the host-computed version is passed as a build argument and the app inside the image reports that same
  version

### Requirement: Base image refresh
When the base image referenced by the `Dockerfile` changes, the system SHALL rebuild and republish the `:latest`
application image with the patched base, via CI delegating to the single Nuke image-publish path, so the published
`:latest` does not accumulate unpatched base CVEs. Released version tags (`v*`) SHALL remain immutable — a base
patch updates `:latest`, and a tagged release picks up the patched base at publish time.

#### Scenario: Base-image update republishes latest
- **WHEN** a change to the `Dockerfile` base image (e.g. a merged Renovate digest bump) lands on the default branch
- **THEN** the application image is rebuilt and `:latest` is republished with the patched base (multi-architecture)

#### Scenario: Released tags stay immutable
- **WHEN** the base image is patched between releases
- **THEN** existing `v*` image tags are not re-pushed; the patched base reaches versioned images at the next release

### Requirement: HTTPS listener
The host SHALL listen for HTTPS on a configurable port and select the server certificate per request via
SNI using the certificate store, falling back to the default certificate for unknown hosts. HTTP SHALL
remain available for ACME HTTP-01 challenges and HTTP→HTTPS redirects.

#### Scenario: HTTPS served with the host certificate
- **WHEN** a certificate for `app.local` is present and a client connects over HTTPS with SNI `app.local`
- **THEN** the connection is served with that certificate

#### Scenario: Unknown host falls back to the default certificate
- **WHEN** a client connects over HTTPS for a host with no stored certificate
- **THEN** the default (self-signed) certificate is presented

#### Scenario: HTTP remains available
- **WHEN** the HTTPS listener is enabled
- **THEN** the host still accepts HTTP requests (for ACME challenges and redirects)

### Requirement: PROXY protocol edge listeners
When `Server:EnableProxyProtocol` is enabled, the system SHALL expect a PROXY protocol header (version 1 text
or version 2 binary) at the start of each edge connection, parse it, and set the connection's remote endpoint
to the client address it carries before HTTP processing, so forwarded headers reflect the real client. A
header that carries no client address (`UNKNOWN` in v1, `LOCAL`/`UNSPEC` in v2) SHALL leave the transport peer
unchanged, and a malformed header SHALL abort the connection. When the option is disabled, connections SHALL be
processed unchanged, expecting no header.

#### Scenario: Real client address recovered from the header
- **WHEN** the option is enabled and a connection begins with a valid PROXY protocol header for client `C`
- **THEN** the connection's remote endpoint is `C`, so `X-Forwarded-For`/`X-Real-IP` carry `C` and the HTTP
  request after the header is processed normally

#### Scenario: Address-less header leaves the peer unchanged
- **WHEN** the option is enabled and a connection begins with a `LOCAL`/`UNKNOWN` header (e.g. a balancer health check)
- **THEN** the connection's remote endpoint is left as the transport peer

#### Scenario: Disabled option is a no-op
- **WHEN** `Server:EnableProxyProtocol` is disabled
- **THEN** connections are processed exactly as before, with no PROXY header expected

### Requirement: HTTPS port is exposed for containers
The container image and reference Compose stack SHALL expose and publish the HTTPS port so a deployed
instance is reachable over HTTPS.

#### Scenario: Reference stack exposes HTTPS
- **WHEN** the reference Compose stack is started
- **THEN** the HTTPS port is published and reachable

### Requirement: Options bound from configuration
The host SHALL bind its runtime options from configuration (appsettings and environment variables),
covering at least TLS/ACME (`AcmeDirectoryUri`, `AcceptTermsOfService`, contact email, certificate
directory, renewal margins), security headers, the Docker discovery endpoint, the admin API key, and the
shutdown timeout. Safe defaults SHALL apply when a value is absent (the ACME directory defaults to the
staging endpoint until explicitly overridden).

#### Scenario: Production ACME directory via configuration
- **WHEN** `Tls:AcmeDirectoryUri` and `Tls:AcceptTermsOfService` are set in configuration
- **THEN** the ACME client uses that directory and terms without any code change

#### Scenario: Security headers configurable
- **WHEN** security header options are provided in configuration
- **THEN** the security middleware emits headers according to those values

#### Scenario: Safe defaults when unset
- **WHEN** no ACME directory is configured
- **THEN** the ACME client uses the Let's Encrypt staging endpoint by default

### Requirement: End-to-end test suite
The system SHALL provide an end-to-end test suite that boots DockYarp and labeled backend containers on a
real Docker daemon (via .NET Aspire) and asserts, over HTTP, that containers are discovered and requests are
proxied according to their labels. The suite SHALL be runnable through the build pipeline and included in
release validation, and SHALL be excluded from the ordinary build/test so the default developer loop needs no
Docker daemon. The default build/test SHALL exclude the end-to-end suite by project (not by a category filter
that matches no tests) so it runs deterministically.

#### Scenario: End-to-end suite excluded from the default build
- **WHEN** the default build/test target runs (no explicit end-to-end request)
- **THEN** the end-to-end tests do not execute and no Docker daemon is required

#### Scenario: Default build/test runs deterministically
- **WHEN** the default build/test target runs
- **THEN** it runs the unit/integration test projects (excluding the end-to-end project) and does not fail on
  projects that match no tests

#### Scenario: End-to-end suite runnable on demand
- **WHEN** the dedicated end-to-end target is invoked with a Docker daemon available
- **THEN** the `dockyarp:local` image is built, the Aspire application boots DockYarp with the labeled
  backend containers, and the end-to-end tests run against it

#### Scenario: Release validation runs the end-to-end suite
- **WHEN** the release target runs
- **THEN** it depends on both the ordinary test suite and the end-to-end suite, so a release is validated only
  when the end-to-end tests also pass

#### Scenario: Discovered backend is reachable through the proxy
- **WHEN** the Aspire application is running and a request is sent to DockYarp with a backend container's
  `VIRTUAL_HOST`
- **THEN** the request is proxied to that backend and returns its response

### Requirement: End-to-end TLS coverage
The end-to-end test suite SHALL additionally cover TLS: with a local ACME certificate authority in the Aspire
distributed system, it SHALL assert that DockYarp provisions a real certificate over the ACME HTTP-01
challenge, serves it over HTTPS, falls back to the self-signed certificate for unknown hosts, and enforces
mutual TLS on hosts that require a client certificate. The harness SHALL start DockYarp independently of
the ACME authority's readiness (DockYarp provisions in the background with retries). The harness SHALL make
DockYarp's HTTP-01 challenge endpoint reachable from the ACME authority by the certificate host name on the
challenge port, so certificates are actually issued in-cluster. These scenarios SHALL remain part of the
end-to-end suite (runnable on demand and in release validation, excluded from the default build).

#### Scenario: Certificate provisioned over ACME
- **WHEN** a backend labeled with `LETSENCRYPT_HOST` is discovered and a client connects over HTTPS with that
  host as the SNI name
- **THEN** DockYarp serves a certificate issued by the local ACME authority for that host (not the
  self-signed fallback)

#### Scenario: Unknown host uses the self-signed fallback
- **WHEN** a client connects over HTTPS for a host with no provisioned certificate
- **THEN** the self-signed fallback certificate is presented

#### Scenario: HTTP is redirected to HTTPS
- **WHEN** a certificate is available for a host whose HTTPS method is redirect and an HTTP request is sent for it
- **THEN** the response redirects the client to the HTTPS URL

#### Scenario: Mutual TLS is enforced
- **WHEN** a host requires a client certificate (`DOCKYARP_CLIENT_CERT=required`)
- **THEN** a request presenting a certificate that chains to the configured client CA is proxied, while a
  request presenting none is rejected

#### Scenario: DockYarp starts without waiting for the ACME authority
- **WHEN** the distributed application starts and the ACME authority is not yet ready
- **THEN** DockYarp still starts and becomes healthy, provisioning certificates in the background once the
  authority is reachable

#### Scenario: The ACME authority reaches the HTTP-01 challenge endpoint
- **WHEN** DockYarp requests a certificate for a TLS host and the authority validates the HTTP-01 challenge
- **THEN** the authority resolves that host name to DockYarp's challenge endpoint on the challenge port and
  retrieves the challenge token, so the certificate is issued

### Requirement: Non-root Docker API access
The system SHALL support Docker discovery from a non-root DockYarp container through two documented modes:
a socket proxy (the recommended default) exposing a read-only Docker API over TCP, and, as an alternative,
mounting the Docker socket while granting the container membership of the socket's owning group.

#### Scenario: Access via a socket proxy (default)
- **WHEN** DockYarp is configured with a Docker endpoint pointing at a socket-proxy service
- **THEN** discovery reaches the Docker API over that endpoint without DockYarp mounting the socket or
  running as root

#### Scenario: Access via group membership (alternative)
- **WHEN** the alternative example mounts the Docker socket and adds the container to the socket's owning group
- **THEN** the non-root DockYarp process can read the socket and discovery works

### Requirement: End-to-end runtime security assertions
The end-to-end suite SHALL additionally assert, against the real runtime, security behaviors that cannot be
observed in-process: that a proxied response does not expose a `Server` header, and that an HTTP→HTTPS
redirect uses status 308. These assertions SHALL be integrated into existing scenarios that already exercise
the corresponding flow, not a synthetic combined test.

#### Scenario: Proxied response omits the Server header
- **WHEN** a request is proxied to a discovered backend over the real runtime
- **THEN** the response carries no `Server` header

#### Scenario: HTTP→HTTPS redirect uses 308
- **WHEN** an HTTP request is sent for a certificate-backed host whose HTTPS method is redirect
- **THEN** the response status is 308 and the `Location` is the HTTPS URL for the same host and path

### Requirement: End-to-end diagnostics capture
The end-to-end suite SHALL capture each Aspire resource's logs to durable per-resource files under an
artifacts directory during the run, so a failure can be diagnosed after the containers are torn down. Capture
SHALL write to files (not the test console), and the `E2E` build target SHALL surface the diagnostics
directory when the run fails.

#### Scenario: Resource logs persist after teardown
- **WHEN** the end-to-end run finishes or fails and the containers are disposed
- **THEN** each resource's logs remain available in a per-resource file under the artifacts log directory

#### Scenario: Failure surfaces the diagnostics location
- **WHEN** the `E2E` target's test run fails
- **THEN** the build output reports the diagnostics log directory

### Requirement: Plaintext HTTP endpoint protocol
The plaintext HTTP endpoint (which serves ACME HTTP-01 challenges and HTTP→HTTPS redirects) SHALL negotiate
HTTP/1.1 only — HTTP/2 requires TLS — while the HTTPS endpoint retains its configured protocols (HTTP/1.1 and
HTTP/2). This avoids Kestrel's spurious "HTTP/2 is not enabled … TLS is not enabled" startup warning.

#### Scenario: HTTP endpoint is HTTP/1.1 only
- **WHEN** DockYarp starts with a plaintext HTTP endpoint and a TLS HTTPS endpoint
- **THEN** the HTTP endpoint is configured for HTTP/1.1 only and no HTTP/2-without-TLS warning is emitted

#### Scenario: HTTPS endpoint keeps HTTP/2
- **WHEN** the HTTPS endpoint is configured
- **THEN** it retains the configured protocols (HTTP/1.1 and HTTP/2)

### Requirement: Persistent state on a non-root-writable volume
DockYarp runs as a non-root user; its persistent state — ACME certificates and Data Protection keys — SHALL be
written to a mounted volume that the non-root app user can write and that survives container recreation, rather
than to the ephemeral container filesystem.

#### Scenario: Non-root app writes the mounted volume
- **WHEN** DockYarp (running non-root) provisions a certificate
- **THEN** the certificate is written to the mounted certificate volume without a permission error

#### Scenario: State survives container recreation
- **WHEN** the container is recreated with the same volume
- **THEN** previously persisted certificates and Data Protection keys are still present

#### Scenario: Data Protection keys are persisted
- **WHEN** DockYarp starts
- **THEN** Data Protection keys are stored under the certificate volume, not the ephemeral default location

### Requirement: At-rest encryption of Data Protection keys is optional and operator-controlled
DockYarp SHALL encrypt its persisted Data Protection key ring at rest when an operator supplies an encryption
certificate, and SHALL NOT require one when no feature depends on Data Protection. When no encryption certificate
is configured, DockYarp SHALL start normally and SHALL NOT emit the "keys may be persisted unencrypted" warning,
because no sensitive payload is protected. When a configured encryption certificate cannot be loaded, startup
SHALL fail with an actionable error rather than silently falling back to unencrypted keys.

#### Scenario: Key ring encrypted when a certificate is configured
- **WHEN** DockYarp starts with a Data Protection encryption certificate configured
- **THEN** the persisted key ring is protected with that certificate (encrypted at rest)

#### Scenario: No certificate required by default
- **WHEN** DockYarp starts with no Data Protection encryption certificate configured
- **THEN** it starts without requiring one and does not emit the unencrypted-keys warning

#### Scenario: Misconfigured certificate fails fast
- **WHEN** a Data Protection encryption certificate is configured but cannot be loaded (missing file or wrong
  password)
- **THEN** startup fails with an actionable error instead of persisting keys unencrypted

### Requirement: End-to-end restart-persistence assertion
The end-to-end suite SHALL assert, against the real runtime, that persisted state survives a container
recreation: after a certificate is provisioned for a TLS host, restarting the DockYarp container against the
same certificate volume SHALL serve the same certificate (reused from the volume) rather than re-provisioning a
new one. This assertion SHALL be integrated into the existing end-to-end suite (runnable on demand and in release
validation, excluded from the default build).

#### Scenario: Provisioned certificate is reused after a container restart
- **WHEN** DockYarp has provisioned a certificate for a TLS host and its container is restarted against the same
  certificate volume
- **THEN** after the container is healthy again it serves the same certificate (same thumbprint) for that host,
  proving the persisted state survived the recreation

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

### Requirement: Supply-chain security scanning
The project SHALL run static code analysis (CodeQL) on every pull request and on a recurring schedule, SHALL
review dependency changes on every pull request for known vulnerabilities, and SHALL generate a Software Bill
of Materials and run a vulnerability scan against every published container image, failing the build on a
Critical or High severity finding unless explicitly allowlisted.

#### Scenario: CodeQL runs on a pull request and on a schedule
- **WHEN** a pull request is opened or updated, or the weekly schedule fires
- **THEN** CodeQL analyzes the C# codebase and surfaces any findings as code scanning alerts

#### Scenario: Dependency review flags vulnerable or newly-added dependencies
- **WHEN** a pull request changes a dependency manifest
- **THEN** the pull request check fails if a changed dependency introduces a known vulnerability at or above the
  configured severity threshold

#### Scenario: Every published image is scanned and gets an SBOM
- **WHEN** the application image is published (release or edge channel)
- **THEN** a Software Bill of Materials is generated for that image and a vulnerability scan runs against it,
  failing the workflow on a Critical or High severity finding that is not allowlisted

#### Scenario: An accepted finding can be allowlisted
- **WHEN** a vulnerability finding is deliberately accepted (no fix available, not exploitable in this context)
- **THEN** it can be added to a tracked allowlist so it no longer fails the build, without disabling the scan

