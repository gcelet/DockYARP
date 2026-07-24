## MODIFIED Requirements

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
