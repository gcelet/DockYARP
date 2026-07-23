## Why

The first real runs of the Aspire e2e suite all failed at `OneTimeSetUp` with
`DistributedApplicationException: Stopped waiting for resource 'dockyarp' to become healthy because it
failed to start.` Diagnosing against a live daemon (running the AppHost directly + `docker ps -a`) revealed
**two independent reasons DockYarp never started**, both in the test harness (the app itself boots fine
with the full config, verified with `docker run`):

1. **`WaitFor(stepca)`** gated DockYarp on step-ca becoming healthy, but the `smallstep/step-ca` image's
   health check stays `starting` — so DockYarp was never created.
2. After removing that gate, DockYarp was still absent from `docker ps -a`: it is the only container
   carrying `--network-alias` runtime args (added for ACME HTTP-01 host resolution), and DCP fails to create
   a container with those args. Every other container came up; only DockYarp did not.

## What Changes

- **Remove `WaitFor(stepca)`.** DockYarp provisions ACME in the background with retries, so it must start
  and serve even when the CA is not ready (also matches production). `WaitFor(dockerproxy)` stays (the proxy
  has no health check, so it is ready as soon as it is running).
- **Remove the `--network-alias` runtime args** from the DockYarp container (and the now-unused
  `BackendCatalog.TlsHosts`). They broke DCP container creation. A DCP-compatible way to make step-ca resolve
  `LETSENCRYPT_HOST` back to DockYarp for HTTP-01 is deferred to a follow-up; the TLS env/mounts stay in
  place so the rest of the TLS wiring is ready.

Net effect: DockYarp starts in the e2e, discovery works through the socket proxy, and the HTTP scenarios
can run. TLS scenarios that require a real ACME certificate remain to be unblocked by the HTTP-01 follow-up.

## Capabilities

### Modified Capabilities
- `deployment`: the e2e harness starts DockYarp independently of the ACME authority's readiness and without
  runtime args that DCP rejects.

## Impact

- **Test harness only**: `tests/DockYarp.E2E.AppHost` (`Program.cs`, `BackendCatalog.cs`). No product/`src`
  change (the app boots correctly with this config — confirmed by a standalone `docker run`).
- **Unblocks**: DockYarp startup + discovery + the HTTP e2e scenarios.
- **Deferred**: ACME HTTP-01 host resolution (step-ca → DockYarp), hence the ACME-cert-dependent TLS
  scenarios.
- **Owning agent**: AG-DEP.
