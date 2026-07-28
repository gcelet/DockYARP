# Design — test-restart-state-persistence

## Goal
Prove, end to end, the deployment spec scenario "State survives container recreation": after the DockYarp
container is recreated against the same `/certs` volume, previously persisted state is reused rather than
regenerated.

## What to assert (and why it is robust)
The **served certificate's thumbprint** is the cleanest observable proof of reuse: it is captured over the wire
(TLS handshake) with no fragile container-written-file reads. If the persisted certificate survives the restart
and DockYarp reloads it, the same thumbprint is served; if the volume were wiped, DockYarp would re-provision a
new certificate (different thumbprint) or briefly serve the self-signed fallback.

This exercises the **same persistent volume** that also carries the Data Protection key ring, so cert reuse is a
faithful proxy for "state survives recreation". We deliberately do **not** read the DP key files from the host to
assert on them: the container writes them as a different (non-root) uid, so their host-side readability is
environment-dependent and would make the test flaky. The already-shipped e2e diagnostics (absence of
`FileSystemXmlRepository[60]`) plus this cert-reuse assertion together cover the persistence story.

## The renewal-churn problem
`CertificateProvisioningService.NeedsCertificate` renews when `NotAfter - now <= RenewBeforeExpiry`. step-ca
issues ~24h certificates, while `RenewBeforeExpiry` defaults to 30 days, so every provisioning pass renews — and
the e2e runs a pass every `CheckInterval` (5s, kept low so provisioning retries after discovery). Under that
default the served thumbprint changes every ~5s, independent of any restart, which would make a thumbprint-reuse
assertion flaky.

**Mitigation**: set `Tls__RenewBeforeExpiry` in the e2e AppHost to a value below step-ca's certificate lifetime
(e.g. one minute). A freshly provisioned ~24h certificate is then not "near expiry", so it is never renewed
during a run and the thumbprint is stable — which is also more deterministic for the existing TLS scenarios. We
do **not** touch `CheckInterval` (its 5s value is what lets provisioning retry after discovery races the startup
pass).

**Alternative considered (deferred)**: the more coherent fix is to keep DockYarp's realistic default renewal
margin and instead make step-ca issue certificates longer than 30 days, so no renewal is due during a run. step-ca
exposes no duration env — the only lever is `ca.json`'s `authority.claims`, which is written at init and read
only at startup (no hot-reload). Applying it before DockYarp's first issuance means restructuring the working
step-ca setup (split init/serve or patch-and-restart before DockYarp starts), which cannot be validated locally
(Docker is only available in the e2e environment). Tracked as backlog `e2e-stepca-long-cert-duration`.

## Restart mechanics
- Restart the `dockyarp` resource with `ResourceCommandService.ExecuteCommandAsync(name,
  KnownResourceCommands.RestartCommand, ct)` (resolved from `application.Services`), then
  `application.ResourceNotifications.WaitForResourceHealthyAsync("dockyarp", ct)`. DCP recreates the container
  against the same resource spec, so the `/certs` bind mount and the published HTTPS endpoint are preserved (the
  test's captured `HttpsBaseAddress` stays valid).
- The e2e boots **one shared** AppHost (`[SetUpFixture]`/`[OneTimeSetUp]`), and NUnit runs tests sequentially by
  default (no `[Parallelizable]`), so restarting the shared proxy will not race a concurrent test. The helper
  waits for health before returning, leaving the proxy usable for any later test.

## Test flow (`RestartPersistenceTests`, `[Category("EndToEnd")]`)
1. Poll `https://tls.local/` until the served certificate is ACME-issued (issuer marker); capture thumbprint T1.
   **Assert** it is ACME-issued — a timed-out poll must not silently return the self-signed fallback, or there is
   nothing persisted to reuse and the test is meaningless.
2. Restart the proxy and wait for healthy.
3. Poll `https://tls.local/` again until an ACME certificate is served; capture thumbprint T2.
4. Assert `T2 == T1` — the persisted certificate was reused across the container recreation.

**Ordering / provisioning latency**: NUnit orders classes alphabetically, so `RestartPersistenceTests` runs
*before* `TlsTests` — it is the first test to await `tls.local`'s provisioning and therefore bears the full cold
ACME latency (observed up to ~75s: HTTP-01 retries every `CheckInterval`). The pre-restart wait uses a generous
budget (`ProvisionPollSeconds`) so a genuine certificate exists before the restart; the post-restart wait is
shorter since the certificate is reloaded from disk.

## Risks
- If `ExecuteCommandAsync(RestartCommand)` returned before the container actually cycled, `WaitForResourceHealthy`
  could observe the stale-healthy snapshot. In practice the restart command performs the stop/start before
  completing; if this proves flaky, harden by waiting for a state transition (Running → not-running → Running)
  before polling. Documented so a future maintainer knows the lever.
- Endpoint stability across restart depends on DCP keeping the host-facing proxy port; this is Aspire's
  designed behavior (the endpoint proxy persists while the container behind it restarts).
