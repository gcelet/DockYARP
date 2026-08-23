## 1. Core model + label mapping (AG-AT, AG-DD)

- [x] 1.1 Add `AcmeChallengeType` enum (`Http01`, `Dns01`) to `DockYarp.Core.Models` and a
      `ChallengeType` property to `HostTlsMetadata` (default `Http01`), verified by a compile and existing
      `HostTlsMetadata` tests still passing unchanged.
- [x] 1.2 Map the `DOCKYARP_ACME_CHALLENGE` label (`http-01`/`dns-01`, case-insensitive) into
      `HostTlsMetadata.ChallengeType` in `DockYarp.Docker`'s label mapper, falling back to `Http01` with a
      logged warning for an unrecognized value, verified by unit tests covering `http-01`, `dns-01`,
      absent, and an unrecognized value. 151/151 DockYarp.Docker.Tests green.

## 2. RFC 2136 DNS UPDATE + TSIG (AG-AT)

- [x] 2.1 Implement a minimal DNS message writer for the RFC 2136 UPDATE opcode (header, zone section, one
      add/delete for a TXT RRset, additional section carrying a TSIG record per RFC 8945), verified by
      structural unit tests (`DnsUpdateMessageTests`) that parse the produced bytes back apart and assert
      each field against the RFC's own layout — not a claimed "known-good vector" (none was available to
      cite with confidence), but real field-by-field verification.
- [x] 2.2 Implement TSIG signing (`HMACSHA256` default, matching `Tls:DnsUpdateTsigAlgorithm`) over the
      canonical signed data, verified by a unit test that independently recomputes the HMAC over the same
      canonical bytes and asserts it matches the embedded MAC — real, not just structural.
- [x] 2.3 Implement `Rfc2136DnsChallengeProvider : IDnsChallengeProvider` (new interface in
      `DockYarp.Tls`) sending the signed UPDATE message over UDP with TCP fallback on truncation (standard
      DNS behavior), verified by unit tests against a real local UDP listener (loopback, ephemeral port)
      exercising the actual send/receive/RCODE-interpretation code path for both a success and a REFUSED
      response.
- [x] 2.4 Add `TlsOptions.DnsUpdateServer`/`DnsUpdateZone`/`DnsUpdateTsigKeyName`/`DnsUpdateTsigKeySecret`/
      `DnsUpdateTsigAlgorithm` (all optional; unset disables DNS-01 entirely), verified by a unit test that
      calling the provider without complete configuration throws a clear, actionable exception (lazy
      validation, on first use — not at construction, so only the affected host's provisioning fails; see
      section 4). 105/105 DockYarp.Tls.Tests green (includes the new `DnsUpdateMessageTests` and
      `Rfc2136DnsChallengeProviderTests`).

## 3. DNS-01 challenge flow (AG-AT)

- [x] 3.1 Extend `CertesAcmeClient.RequestCertificateAsync` to branch on `HostTlsMetadata.ChallengeType`
      (threaded through a new `DesiredCertificate.ChallengeType` and `IAcmeClient`'s signature): `Dns01`
      uses `authorization.Dns()` + `acme.AccountKey.DnsTxt(challenge.Token)`, publishing via
      `IDnsChallengeProvider` before `challenge.Validate()` and removing the record in a `finally` block
      (mirroring the existing HTTP-01 `finally`/`challenges.Remove` shape, now split into
      `CompleteHttpChallengeAsync`/`CompleteDnsChallengeAsync`). Not unit-tested (same as the existing
      HTTP-01 order/authorization flow — confirmed via `CertesAcmeClientTests.cs`, which only tests
      `BuildLoadedCertificate`; `AcmeContext`/`NewOrder`/`Authorizations` are concrete Certes types, not
      mocked anywhere in this codebase) — verified by the e2e test in section 5 instead. Full solution
      builds clean; 106/106 DockYarp.Tls.Tests, 151/151 DockYarp.Docker.Tests green.
- [x] 3.2 Extend `CertesAcmeClient`'s order construction so a `CertificateHost` starting with `*.` orders
      exactly that one wildcard identifier (no implicit bare-domain identifier — see `design.md`
      "A wildcard order requests exactly one identifier"). Free consequence of 3.1's design, not separate
      code: `acme.NewOrder([host])` already passes `host` through unchanged (e.g. `"*.example.com"`) as the
      sole identifier — nothing to add.
- [x] 3.3 In `CertificateProvisioningService.ReconcileAsync`, strip a leading `*.` from the desired host
      before calling `certificates.Save(...)` (and before the pre-existing `NeedsCertificate` lookup too —
      a real bug caught while implementing: the renewal check would otherwise never find the
      parent-domain-keyed certificate and re-provision on every reconcile pass), verified by
      `WildcardHostIsStoredUnderParentDomain` asserting the cert lands under `example.com`, never under the
      literal `*.example.com`, while the ACME order itself still requests the literal wildcard identifier.

## 4. Configuration surface fail-fast (AG-AT)

- [x] 4.1 When a host resolves to `Dns01` but `TlsOptions`' RFC 2136 fields are incomplete, provisioning
      for that host SHALL fail with a clear error while other hosts continue unaffected (per the spec delta's
      "DNS provider configuration (RFC 2136)" requirement), verified by
      `MisconfiguredDns01HostFailsAloneWhileOthersProvisionNormally` — a DNS-01 host and an HTTP-01 host in
      the same reconcile pass, only the DNS-01 one fails. 107/107 DockYarp.Tls.Tests green.

## 5. End-to-end coverage (AG-AT, AG-DEP)

- [x] 5.1 Add a `bind9` container (ISC's official image, `internetsystemsconsortium/bind9:9.20`) to
      `tests/DockYarp.E2E.AppHost/Program.cs`, authoritative for `dns01.example` (`allow-update` gated by a
      TSIG key) and forwarding everything else to Docker's embedded DNS (127.0.0.11) so step-ca's other
      resolution (HTTP-01 aliases) keeps working. Config staged by `TlsHarness.PrepareDnsZone()` and copied
      into the container's own writable layer at startup (avoids host-bind-mount write-permission issues
      entirely). Verified live: the DNS-01 e2e test below passing IS the end-to-end proof this accepts a
      real authenticated update.
- [x] 5.2 Point the `stepca` container's DNS resolver at the `bind9` container. **Real bug found and fixed,
      not assumed working**: the resolver-rewrite (`echo ... > /etc/resolv.conf`) initially failed with
      `Permission denied` — confirmed live via a standalone `docker run` that the image's Dockerfile sets
      `USER step` (uid 1000) from the start, and `/etc/resolv.conf` is `root:root` mode `0644`. Fixed with
      `.WithContainerRuntimeArgs("--user", "root")` on the `stepca` resource (test-only container, no
      production/security impact) — confirmed live this lets the write succeed and step-ca's DNS-01
      validation genuinely resolves the test zone.
- [x] 5.3 Configure the shared `dockyarp` test resource with `Tls__DnsUpdateServer`/`DnsUpdateZone`/TSIG
      env vars pointing at the `bind9` container, verified by `DockYarp.App` starting successfully with
      DNS-01 configuration present (confirmed via the passing e2e test below).
- [x] 5.4 Add `AcmeWildcardCertificate_IsProvisionedViaDns01` to `TlsTests.cs`: a host with
      `DOCKYARP_ACME_CHALLENGE=dns-01` and a wildcard `LETSENCRYPT_HOST=*.dns01.example`, asserting the
      certificate is issued and a request to `sub.dns01.example` is served over TLS with that certificate.
      **Passing for real** against the actual `bind9` + `stepca` containers (no mocking) — confirmed via
      `dockyarp.log`: `Provisioned certificate for *.dns01.example.`, then the test's own assertions on the
      served certificate's issuer/subject passed.
- [x] 5.5 Update `docs/testing.md`'s e2e coverage map with the new test row, verified by the row matching
      the project's existing table format.

## 6. Documentation (AG-DOC, AG-AT)

- [x] 6.1 Document `DOCKYARP_ACME_CHALLENGE`, the wildcard-host convention, and the RFC 2136
      `Tls:DnsUpdate*` options in `docs/tls-acme.md`, `docs/labels-reference.md`, and
      `docs-site/content/en/docs/configuration.md`, verified by names/defaults matching the actual code
      (per AGENTS.md's doc-update requirement for a user-facing change). Also updated `features.md` (new
      capability callout) and, per the doc-audit habit, fixed two stale "DNS-01 not supported" mentions
      found by grepping the whole doc tree: `docs/architecture.md`'s known-gaps list and
      `migrating-from-nginx-proxy.md`.

## 7. Full validation (AG-AT)

- [x] 7.1 `./build.ps1 Test` (or `./build.sh Test`) green, verified by the command's own exit status and
      the new unit tests appearing in the run. All 5 test projects green (373 tests total: Core 41,
      Docker 151, Security 51, Integration 123, Tls 107).
- [x] 7.2 `./build.ps1 E2E` (or `./build.sh E2E`) green, including the new DNS-01/wildcard e2e test,
      verified by the full suite passing with zero orphaned Docker resources afterward. **Real regression
      found and fixed during this task**: the first full-suite run showed 4 failures
      (`AcmeCertificate_IsProvisionedForHost`, `AcmeCertificate_ChainIncludesIntermediate`,
      `HttpRequest_RedirectsToHttps`, `ProvisionedCertificate_IsReusedAfterRestart`) — all pre-existing
      HTTP-01 tests, not the new DNS-01 one (which passed even then). Root cause confirmed via
      `stepca.log`: `"error":{"type":"urn:ietf:params:acme:error:connection","detail":"The server could not
      connect to validation target"}` — BIND9's `forwarders { 127.0.0.11; }` (Docker's embedded DNS) wasn't
      actually resolving the HTTP-01 socat aliases (`tls.local` etc.) once step-ca's resolver pointed
      exclusively at BIND9, because BIND9's default DNSSEC validation was rejecting the forwarded (unsigned)
      answers. Fixed with `dnssec-validation no;` in `TlsHarness.PrepareDnsZone()`'s `named.conf` template —
      standard requirement when forwarding to a non-DNSSEC-aware resolver. Reran: **42/42 e2e green**,
      confirmed no HTTP-01 regression alongside the working DNS-01 flow.
