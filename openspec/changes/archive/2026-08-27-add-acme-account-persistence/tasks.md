## 1. Account key load/persist logic (AG-AT)

- [x] 1.1 Add a small internal helper (e.g. `AcmeAccountKeyStore`) in `src/DockYarp.Tls` that, given
  `IFileSystem`, `CertificateDirectory`, the resolved contact email, and `AcmeDirectoryUri`: resolves the
  (email, endpoint)-scoped path
  (`{CertificateDirectory}/acme/{email}/{directoryUri.Host}/{directoryUri.AbsolutePath segments...}/account.key`,
  splitting the URI path into nested folder segments the way acme.sh's own `CA_HOME/<host>/<path>` layout
  does, nested under an email folder the way the real acme-companion installation investigated for this
  change does), returns the existing key's PEM bytes if present at that path, or generates a new EC P-256
  key, persists it (creating any missing folder segments), and returns it if absent — mirroring
  `FileCertificateStore`'s existing `IFileSystem` usage for testability.
- [x] 1.2 On load, if `ECDsa.ImportFromPem` fails, attempt `RSA.ImportFromPem` purely to produce a precise
  "found an RSA key, only EC P-256 is supported for import" error; otherwise raise a clear
  "unreadable/corrupt account key file" error. Verify via unit tests in `AcmeAccountKeyStoreTests.cs` using
  `System.IO.Abstractions.TestingHelpers`' fake file system: (a) no file present -> a new key is generated and
  persisted, (b) an existing EC PEM file -> its exact key material is returned unchanged, (c) an RSA PEM file
  -> the specific RSA-detected error, (d) a corrupt file -> the generic unreadable-key error, (e) two
  different `AcmeDirectoryUri` values (same email) resolve to two distinct, independent key files/paths, (f)
  two different contact emails (same endpoint) likewise resolve to two distinct, independent key files/paths.
  Done — 6/6 passing. `ECDsa.ImportFromPem` throws `ArgumentException` (not `CryptographicException`) on an
  unparseable input, so `Import`/`IsRsaKey` catch both.

## 2. Wire persistence into AcmeClient (AG-AT)

- [x] 2.1 Replace `AcmeClient.RequestCertificateAsync`'s `ECDsa.Create(ECCurve.NamedCurves.nistP256)` account
  key generation with a call into the new key store (load-or-generate-and-persist), keyed by the method's
  already-resolved `contact` email and `options.AcmeDirectoryUri`, keeping every other call in the method
  unchanged (a fresh `ECDsa` instance built from the persisted key material, not a shared instance — see
  `design.md`'s thread-safety decision). `AcmeClient` now takes an `IFileSystem` (already DI-registered in
  `TlsServiceCollectionExtensions`) as a fourth constructor parameter.
- [x] 2.2 Same-key-reuse and different-email-independence are verified at the `AcmeAccountKeyStore` level
  (`DifferentContactEmails_ResolveToIndependentKeys`, `ExistingEcKey_IsReturnedUnchanged` in
  `AcmeAccountKeyStoreTests.cs`) rather than through `AcmeClient`/a fake ACME backend: `AcmeClient` itself is
  established as integration-only, not unit-testable, without a live CA (see its own class remark) — the
  account-key sourcing logic that actually needs this proof is fully isolated in the key store, so testing it
  there gives the same guarantee without needing a fake ACME HTTP server.

## 3. E2E coverage (AG-AT)

The e2e fixture (`tests/DockYarp.E2E.AppHost/Program.cs`) sets one global `Tls__ContactEmail=e2e@dockyarp.local`
and `Tls__AcmeDirectoryUri=https://stepca:9000/acme/acme/directory` for every host — so `tls.local` and
`mtls.local` (both already real-ACME-provisioned by the existing suite, per `TlsTests.cs`) resolve to the same
(email, endpoint) pair and therefore the same persisted account key path,
`{CertsDirectory}/acme/e2e@dockyarp.local/stepca/acme/acme/directory/account.key`. Prefer assertions that ride
on provisioning already happening in the suite over adding new real ACME round trips (each one is slow — see
the E2E runtime baseline in `docs/testing.md`).

- [x] 3.1 Extend `RestartPersistenceTests.cs` (same pattern as its existing certificate-thumbprint check) with
  an assertion that reads the persisted account key file's bytes before and after
  `AspireAppHostFixture.RestartProxyAndWaitHealthyAsync`, and asserts they're byte-identical — proving the
  account key (and therefore the ACME account) survived the restart rather than being regenerated. Update
  `docs/testing.md`'s coverage map alongside it, per `AGENTS.md`'s testing-pyramid rule.
- [x] 3.2 Added `TlsTests.AcmeAccountKey_IsSharedAcrossIndependentlyProvisionedHosts`: after `tls.local`
  (HTTP-01) and the `sub.dns01.example` wildcard order (DNS-01 — a genuinely separate order, not just a
  second host) both complete, asserts the persisted account key file's bytes are unchanged between the two —
  proving reuse across two independently-provisioned hosts sharing the fixture's one contact email. No new
  ACME round trip needed; both orders are already exercised elsewhere in the suite. The resolved path itself
  now lives in `E2EPaths.AcmeAccountKeyFile` (shared with 3.1's usage) rather than duplicated per test file.
- [x] 3.3 Decided **not** to add a second-email host to the AppHost fixture: the specific regression
  email-scoping guards against is already fully covered by
  `AcmeAccountKeyStoreTests.DifferentContactEmails_ResolveToIndependentKeys` (fast, deterministic), and a real
  e2e host would only add ACME round-trip cost without proving anything the unit test doesn't already —
  revisit only if a real bug in this area ever surfaces that the unit test wouldn't have caught.
- [x] 3.4 Covered at the unit level instead of e2e/integration:
  `AcmeAccountKeyStoreTests.PreSeededEcKey_Pkcs8Form_IsImportedAsIs` /
  `.PreSeededEcKey_Sec1Form_IsImportedAsIs` seed a known EC key (in both PEM forms a real ACME client might
  produce) at the resolved path and assert it's imported and reused as-is, not regenerated. The
  server-side "same account" guarantee for a reused key is RFC 8555 `newAccount` idempotency itself, already
  a protocol fact this change relies on throughout — not something specific to the import path that needs
  separate live-CA proof.

## 4. Docs (AG-AT / AG-DOC)

- [x] 4.1 Updated `docs/tls-acme.md`'s "Client maintenance & security" section: replaced the account-related
  gap with a "Persisted ACME account" paragraph describing the resolved mechanism, the email-scoping
  rationale, the import path, and the RSA non-goal; also updated the "not applicable" bullet now that
  persistence has shipped (those RFC 8555 sections are real opportunities now, not moot).
- [x] 4.2 `configuration.md`'s `CertificateDirectory` row now also mentions persisted ACME account keys and
  their subpath shape. Also found and filled a real gap this grep surfaced:
  `migrating-from-nginx-proxy.md` documented copying **certificates** from acme-companion but said nothing
  about the **ACME account** itself — added a new "Your ACME account" subsection under "Certificates and
  rollback" covering locating `acme-companion`'s `account.key`, the target path, the EC-vs-RSA check
  (`openssl pkey ... | head -1`), and what happens if the operator skips this step.

## 5. Verification

- [x] 5.1 `dotnet build DockYarp.slnx` succeeds with 0 errors (only the pre-existing, unrelated ASPIRE010
  warning). `dotnet test DockYarp.slnx --filter "TestCategory!=EndToEnd"` passes: 41+151+51+123+126 = 492
  tests, 0 failures (`DockYarp.Tls.Tests` includes the 8 new `AcmeAccountKeyStoreTests`). The `EndToEnd`
  category (step-ca/Docker required) was not run in this environment — the new/changed e2e tests
  (`RestartPersistenceTests`, `TlsTests.AcmeAccountKey_IsSharedAcrossIndependentlyProvisionedHosts`) compile
  cleanly but still need a real run with Docker before this is considered fully verified.
- [x] 5.2 `openspec validate add-acme-account-persistence --strict` passes.
