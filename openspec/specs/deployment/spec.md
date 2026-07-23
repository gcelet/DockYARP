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
least one sample service reachable through DockYarp by its `VIRTUAL_HOST`.

#### Scenario: Sample service reachable through the proxy
- **WHEN** the reference stack is started and a request is sent to DockYarp with the sample service's `VIRTUAL_HOST`
- **THEN** the request is proxied to the sample service and returns its response

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
Docker daemon.

#### Scenario: End-to-end suite excluded from the default build
- **WHEN** the default build/test target runs (no explicit end-to-end request)
- **THEN** the end-to-end tests do not execute and no Docker daemon is required

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
mutual TLS on hosts that require a client certificate. These scenarios SHALL remain part of the end-to-end
suite (runnable on demand and in release validation, excluded from the default build).

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

