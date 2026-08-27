## ADDED Requirements

### Requirement: ACME account persistence
The system SHALL persist an ACME account key per **(resolved contact email, ACME directory endpoint)** pair
and reuse it for every certificate request and renewal sharing that same pair, rather than registering a new
account per request. A host's resolved contact email is its declared `LETSENCRYPT_EMAIL` (or the
capability's existing fallback to `Tls:ContactEmail` when unset) — unchanged from today's resolution. On
first use of a given (email, endpoint) pair with no persisted account key present for it, the system SHALL
generate one and persist it before making that ACME request. The persisted account key SHALL be stored on
the same operator-mounted volume as other DockYarp-persisted key material (the certificate directory), so it
survives a container restart or redeploy the same way stored certificates do. Changing `Tls:AcmeDirectoryUri`
to a different endpoint, or a host resolving to a different contact email than another host, SHALL use (or
generate) a separate persisted key for that (email, endpoint) pair, without disturbing a key already
persisted for a different pair.

#### Scenario: The same account is reused across requests sharing a contact email
- **WHEN** DockYarp requests a second certificate (a different host, or a renewal) that resolves to the same
  contact email and ACME directory endpoint as a prior request
- **THEN** the same ACME account is used, not a new one — verifiable against the CA by the account's own URL
  staying constant across those requests

#### Scenario: Hosts with different contact emails get independent accounts
- **WHEN** two hosts resolve to different contact emails (whether via distinct `LETSENCRYPT_EMAIL` values, or
  one declaring none and falling back to `Tls:ContactEmail` while the other declares an explicit one)
- **THEN** each host's requests use its own separate persisted account, matching today's behavior where each
  request's declared email is honored on its own account

#### Scenario: Switching ACME directory endpoints does not disturb a previously used one's account keys
- **WHEN** an operator changes `Tls:AcmeDirectoryUri` to a different ACME endpoint than the one previously in
  use
- **THEN** DockYarp generates (or reuses, if one is already present) persisted account keys scoped to the new
  endpoint, and every account key persisted for the previous endpoint remains on disk, untouched

#### Scenario: First run generates and persists an account key
- **WHEN** DockYarp makes its first-ever ACME request for a given (contact email, ACME directory endpoint)
  pair, with no persisted account key yet present for it
- **THEN** an account key is generated and persisted for that pair before the request is made

#### Scenario: A persisted account key survives a restart
- **WHEN** DockYarp restarts with a previously persisted account key present for a given (contact email,
  endpoint) pair
- **THEN** a request resolving to that same pair reuses that same account key (and therefore the same ACME
  account) rather than generating a new one

### Requirement: ACME account import (EC-keyed accounts only)
The system SHALL allow an operator to migrate an existing **EC (P-256)** ACME account by placing that
account's PEM private key at the persisted-account-key location matching the migrating host's resolved
contact email and ACME directory endpoint, before DockYarp's first ACME request for that (email, endpoint)
pair, so DockYarp continues using that account (via RFC 8555 `newAccount` idempotency) instead of registering
a new one. An account key using an algorithm other than EC P-256 (for example RSA, the default for some
third-party ACME clients when no EC key length was explicitly requested at registration) is **not**
supported for import — DockYarp SHALL treat an unsupported key algorithm at that location as a configuration
error, not silently ignore it and generate a new account.

#### Scenario: An imported EC account key is reused instead of generating a new account
- **WHEN** an operator places an existing EC (P-256) ACME account's PEM private key at the persisted-account-
  key location matching a host's resolved contact email and ACME directory endpoint, before DockYarp's first
  ACME request for that pair
- **THEN** DockYarp's first ACME request for that pair reuses that existing account (same account URL as the
  CA already has on record for that key) rather than registering a new one

#### Scenario: An unsupported key algorithm at the import location fails clearly
- **WHEN** a PEM private key using an algorithm other than EC P-256 (for example RSA) is present at the
  persisted-account-key location
- **THEN** DockYarp fails with an actionable error identifying the unsupported algorithm, rather than
  silently generating a new account key
