## MODIFIED Requirements

### Requirement: ACME certificate acquisition
The system SHALL obtain a certificate for each host that declares TLS metadata, using an ACME provider via
the HTTP-01 challenge, and SHALL store the resulting certificate in the certificate store. **Acquisition SHALL
succeed regardless of whether the ACME provider's response includes its own self-signed root certificate — a
root is not required to be present, and its absence SHALL NOT cause any intermediate the provider did return to
be discarded.** When the issued chain includes one or more intermediates, the system SHALL preserve and serve
them during the TLS handshake the same way a provided (PEM/PFX) certificate with a chain is served —
ACME-issued and operator-provided certificates SHALL NOT differ in whether their chain reaches the client,
**and this SHALL hold independently of whether the ACME response also happened to include a self-signed root.**

#### Scenario: Certificate obtained for a labeled host
- **WHEN** a host declares TLS metadata (from `LETSENCRYPT_HOST`/`LETSENCRYPT_EMAIL`) and no current certificate exists
- **THEN** the provisioning service requests a certificate for that host and stores it

#### Scenario: HTTP-01 challenge is answered
- **WHEN** the ACME provider requests validation of a token
- **THEN** the challenge is served at `/.well-known/acme-challenge/{token}` with the expected key authorization

#### Scenario: Private CA whose root is not in the issued chain
- **WHEN** the ACME provider issues a certificate whose chain includes an intermediate but does **not** include
  a self-signed root (a private or custom CA following normal ACME convention — the root is trusted out of
  band, not distributed via the protocol)
- **THEN** the system still stores a usable certificate for the host, **and the intermediate the provider did
  return is preserved, not discarded** — the certificate served for that host is not leaf-only just because no
  root was present in the response

#### Scenario: An ACME-issued intermediate is sent during the handshake
- **WHEN** the ACME provider issues a certificate whose chain includes an intermediate, and that certificate is
  stored and later selected for a TLS handshake
- **THEN** a client trusting only the CA root (not the intermediate, and with no other source for it) can build
  a complete chain from what the server sends during that handshake alone — **this holds whether or not the
  ACME response itself included that root**
