## 1. ACME wire call (AG-AT)

- [x] 1.1 Add a nullable `RevokeCert` (`revokeCert`) field to `AcmeDirectory`
  (`src/DockYarp.Tls/Acme/AcmeDirectory.cs`) — RFC 8555 marks it OPTIONAL in the directory object, unlike
  `NewNonce`/`NewAccount`/`NewOrder`.
- [x] 1.2 Added `RevokeCertificateAsync(byte[] certificateDer, CancellationToken)` to `AcmeHttpClient` (new
  `AcmeRevokeCertificateRequest` model + `AcmeJsonContext` registration for the `{"certificate": ...}` body,
  no `reason`). Throws `InvalidOperationException` when the directory has no `revokeCert` URL. 2 new tests in
  `AcmeHttpClientTests.cs`: signed-POST-to-the-right-URL shape, and the missing-URL error (no signed request
  attempted). 6/6 passing.

## 2. AcmeClient / IAcmeClient (AG-AT)

- [x] 2.1 Added `Task RevokeCertificateAsync(string host, string? email, X509Certificate2 certificate,
  CancellationToken)` to `IAcmeClient`/`AcmeClient`. `AcmeAccountKeyStore` gained `TryLoad` (returns `null`
  instead of generating, unlike `LoadOrCreate` — `LoadOrCreate` refactored to call it). Implementation:
  resolve contact email → `TryLoad` the persisted key → `CreateAccountAsync` first (idempotent, resolves
  `kid` so the revocation request is signed as an authenticated account request, not a bare-jwk one) →
  `RevokeCertificateAsync(certificate.RawData, ...)`.
- [x] 2.2 No-persisted-key case: throws `InvalidOperationException` before any network call (verified by a
  new `AcmeClientTests.RevokeCertificate_WithNoPersistedAccount_ThrowsWithoutAttemptingTheNetwork` test — this
  one *is* unit-testable despite `AcmeClient` otherwise being integration-only, since the check happens before
  `HttpClient` is even constructed).
- [x] 2.3 Added `RevokeCertificateAsync`/`RevocationCount` to `FakeAcmeClient.cs`. Also had to add a trivial
  implementation to 4 more `IAcmeClient` test doubles in `CertificateProvisioningServiceTests.cs`
  (`ScriptedAcmeClient`, `PerHostFailureAcmeClient`, `FailingAcmeClient`, `RendezvousAcmeClient`) that the
  proposal/design didn't call out explicitly — a real interface-change ripple, not scope creep.

## 3. Certificate store removal (AG-AT)

- [x] 3.1 Added `bool Remove(string host)` to `ICertificateStore`/`FileCertificateStore` (deletes
  `.crt`/`.key`/`.pfx`, drops from the in-memory dictionary, disposes) and to `FakeCertificateStore` (only 2
  real implementations in the repo). 3 new unit tests in `CertificateStoreTests.cs`: PEM removal, legacy-PFX
  removal, unknown-host no-op. 28/28 `CertificateStoreTests` passing.
- [x] 3.2 Added `ReprovisionsAfterCertificateIsRemoved` to `CertificateProvisioningServiceTests.cs` — confirms
  `NeedsCertificate`'s existing null-check is sufficient, no new reconcile logic needed. 10/10 passing.

## 4. Admin dashboard trigger (AG-AA)

- [x] 4.1 Added `AllowCertificateRevocation` (`bool`, default `false`) to `AdminApiOptions`, XML doc remark
  explaining the separate-flag rationale.
- [x] 4.2 Decided: a new `ICertificateRevoker` interface (`src/DockYarp.AdminApi/ICertificateRevoker.cs`) —
  bundling into `ICertificateConverter` did read oddly given the "converter" name and the materially different
  consequence. Implemented by `CertificateRevokerAdapter` (`src/DockYarp.App/Observability/`), which resolves
  the host's contact email via `IRouteConfigStore`/`IReservedCertificateHosts`/`TlsDomains.Desired` (mirroring
  `CertificateProvisioningService`'s own desired-hosts resolution, including wildcard-parent-domain stripping)
  before calling `IAcmeClient.RevokeCertificateAsync` and `ICertificateStore.Remove`. DI-registered in
  `ObservabilityServiceCollectionExtensions.cs`.
- [x] 4.3 Mapped `POST /dashboard/certs/{host}/revoke` in `DashboardEndpointMapping.cs`, mirroring
  `PostConvertAsync`/`PostReencryptAsync` exactly.
- [x] 4.4 Added `AllowCertificateRevocation` to `DashboardViewModel`/`GetDashboard` and a "Revoke"
  button/form to `Dashboard.cshtml`, gated on that flag. Added a browser `confirm()` on submit given this
  action's higher consequence (no new server round-trip). 3 new integration tests in
  `AdminObservabilityTests.cs` (default-disabled, invokes-the-revoker, antiforgery-rejected) mirroring the
  existing convert/reencrypt test trio exactly — 20/20 `AdminObservabilityTests` passing.

## 5. Docs (AG-AT / AG-AA / AG-DOC)

- [x] 5.1 Updated `docs/tls-acme.md`'s "Client maintenance & security" section: replaced the revocation gap
  bullet with a resolved "Certificate revocation" paragraph, linking to this change.
- [x] 5.2 Added `AllowCertificateRevocation` to `configuration.md`'s `AdminApi` table and fixed the now-
  inaccurate "only mutating actions" sentence on `AllowCertificateConversion`'s row. Also found (grepping for
  "Convert to PEM"/"mutating" per `AGENTS.md`'s doc-audit habit) two more real gaps beyond the literal task:
  `features.md` was missing the revoke action entirely (added a paragraph matching the download/conversion
  ones already there), and — more significantly — the **live `openspec/specs/admin-api/spec.md`** capability
  spec has a "Read-only admin dashboard" requirement whose own text ("exactly one narrow ... mutating action")
  is a real, load-bearing spec sentence, not just doc prose. This wasn't declared as a capability in the
  original proposal (only `tls-acme` was) — corrected: added `admin-api` to `proposal.md`'s Modified
  Capabilities and a new `specs/admin-api/spec.md` delta (MODIFIED "Read-only admin dashboard" text; ADDED
  "Certificate revocation from the dashboard" requirement, mirroring "Certificate format conversion from the
  dashboard"'s existing structure). Re-validated strict — passes.

## 6. Verification

- [x] 6.1 `dotnet build DockYarp.slnx`: 0 errors (only the pre-existing, unrelated ASPIRE010 warning).
  `dotnet test DockYarp.slnx --filter "TestCategory!=EndToEnd"`: 41+151+51+126+133 = 502 tests, 0 failures
  (`DockYarp.Tls.Tests` +7 new, `DockYarp.IntegrationTests` +3 new vs. the account-persistence change's
  baseline).
- [x] 6.2 Decided **against** the e2e assertion: the e2e AppHost fixture runs `AdminApi__Surface=Api` (no
  dashboard at all — confirmed in `Program.cs`), so exercising the dashboard route would require widening
  `Surface` to `ApiAndDashboard` for the whole shared fixture, a real fixture-risk change out of proportion to
  this one task. The genuinely CA-only-provable fact (the CA accepts our `revokeCert` JWS) reuses
  `AcmeHttpClient.SendSignedAsync`'s exact same signing/nonce/retry machinery already proven against real
  step-ca by every other wire call (`newAccount`/`newOrder`/`finalize`) — only the URL and JSON body shape are
  new, and those are unit-tested against a queued fake handler (`AcmeHttpClientTests`). Revisit if the
  dashboard ever gets its own e2e coverage for an unrelated reason.
- [x] 6.3 `openspec validate add-acme-certificate-revocation --strict` passes (re-run after the `admin-api`
  delta spec addition).
