## Why

`AcmeClient` generates a brand-new ACME account key on every `RequestCertificateAsync` call, including every
renewal (every ~60 days per host by default). Against Let's Encrypt — the realistic default CA for most
nginx-proxy-replacement operators, not just DockYarp's own step-ca-based test/dev usage — this is a real
production risk: LE applies per-account rate limits (failed-validation and new-account-creation among them),
and creating a throwaway account on every renewal, across potentially many hosts over time, is exactly the
pattern LE's own abuse detection exists to flag. It also breaks migration continuity: an operator moving from
nginx-proxy (whose `acme-companion` sidecar persists one ACME account, reused across every certificate it
manages) would have DockYarp silently abandon that account relationship on day one instead of continuing it.

## What Changes

- `AcmeClient` generates its ACME account key **once per (contact email, ACME directory endpoint) pair** and
  persists it (EC P-256 PEM, alongside where DockYarp already persists other operator-facing key material on
  the certificate volume), reusing it for every subsequent `RequestCertificateAsync` call that resolves to
  the same pair, instead of generating a fresh key per call. Scoping by contact email — not just by CA
  endpoint — preserves DockYarp's existing per-host `LETSENCRYPT_EMAIL` behavior (each distinct email still
  gets its own account, unchanged); scoping by endpoint alone would have silently collapsed every host onto
  whichever email happened to create the persisted account first, since RFC 8555 `newAccount` resolves an
  account by JWK, not by the `Contact` field in the request. This relies on `newAccount`'s own idempotency (a
  request whose JWK already has an account returns the existing account rather than creating a new one) — no
  new "does an account exist" lookup is required.
- On first use of a given (email, endpoint) pair (no persisted key yet for it), a key is generated and
  persisted before that ACME request.
- An operator migrating an existing **EC-keyed** nginx-proxy/acme-companion account can drop that PEM key
  into the persisted-account-key location and have DockYarp continue using that same account, rather than
  registering a new one. (An RSA-keyed account — acme.sh's own default when no EC key length was explicitly
  requested — is explicitly **not** supported by this change; DockYarp's JWS signing is ES256-only today.)

## Capabilities

### Modified Capabilities
- `tls-acme`: ACME account creation changes from "a fresh account per certificate request" to "one persisted
  account, reused across every request," with an opt-in path to import an existing EC-keyed account key.

## Impact

- `src/DockYarp.Tls/AcmeClient.cs`: stops generating `accountKey` per call; loads/generates it once (likely
  moved to construction or a lazily-initialized field) and reuses it across calls.
- `src/DockYarp.Tls/TlsOptions.cs`: no new setting — the persisted account key's path is derived from
  `CertificateDirectory`, the resolved contact email, and the already-configured `AcmeDirectoryUri` (one
  persisted key per (email, endpoint) pair, mirroring acme-companion's own real on-disk layout — see
  `design.md`).
- `src/DockYarp.Tls/Acme/AcmeHttpClient.cs`: unaffected in its call pattern — `CreateAccountAsync` already
  relies on `newAccount`'s idempotency implicitly; only the key it's constructed with changes from
  call-scoped to persisted.
- Tests: `tests/DockYarp.Tls.Tests` (account-key persistence/reuse), likely a new e2e assertion that the ACME
  account URL stays constant across two provisioning calls against the real step-ca fixture.
- Docs: `docs/tls-acme.md`'s "Client maintenance & security" section (the gap this change closes).
