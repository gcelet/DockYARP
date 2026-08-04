## ADDED Requirements

### Requirement: ACME HTTP-01 challenge serving
The system SHALL serve ACME HTTP-01 challenges from its token store independently of host routing: a challenge
is answered whenever its token is in the store, even for a host that has no matching route (the store only
holds tokens the system is itself provisioning). Serving SHALL be enabled by default and MAY be disabled via
`Tls:Http01ChallengeEnabled`; when disabled, a request to the challenge path SHALL return 404 instead of the
token.

#### Scenario: Challenge answered regardless of host routing
- **WHEN** an HTTP-01 request arrives for a token present in the store
- **THEN** the key authorization is served, independent of whether the requested host has a route

#### Scenario: Challenge serving disabled
- **WHEN** `Tls:Http01ChallengeEnabled` is `false` and a request reaches the ACME challenge path
- **THEN** the response is 404, even for a token that is present in the store
