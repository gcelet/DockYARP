## Context

See `proposal.md` for motivation; both scenarios' full history (repeated DCP-blocked attempts, the eventual
harness split) lives in the two original backlog stubs this change replaced (folded into
`openspec/backlog/items/e2e-nondcp-network-scenarios.md`) — not re-derived here.

Current code, read directly:
- `BackendAddressResolver.Resolve` (`src/DockYarp.Docker/Discovery/BackendAddressResolver.cs`) only consults
  `hostAddress` when `IsHostNetwork(networkAddresses)` is true; for every other container it returns the
  selected network IP, or empty (skip) when `proxyNetworks.Count > 0` and no shared network exists, or the
  container name when reachability is unknown. **This resolves this change's one open design question**: setting
  `Docker__HostAddress` on the existing shared `dockyarp` AppHost resource cannot affect the unreachable-network
  scenario's own assertion (it's dead code for any non-host-mode container) — no second dedicated proxy instance
  is needed, both scenarios run against the one shared `dockyarp` resource.
- `DiscoveryReconciler` funnels every exclusion reason (`VIRTUAL_HOST is required`, `excluded while Unhealthy`,
  `no reachable network address; not routed`) through the same "skip, not routed" path — confirmed live during
  `add-nondcp-e2e-harness` (the exact log line the unreachable-network scenario will reproduce on purpose). The
  existing `DiscoveryTests.UnhealthyBackend_IsExcluded` test proves the resulting behavior for one skip reason
  (a request to the excluded host falls through to `Routing__DefaultHost`) — the unreachable-network scenario
  mirrors that exact assertion pattern for a different skip reason, not a new one.
- `NonDcpHarness.RunContainerAsync` (`tests/DockYarp.E2E.Tests/NonDcpHarness.cs`) currently takes `image`,
  `labels`, `hostConfig` — no `env`. Both new scenarios need environment variables (`ASPNETCORE_URLS`,
  `BACKEND_ID`), so this change extends it with an optional `env` parameter — anticipated by
  `add-nondcp-e2e-harness`'s own design.md ("`hostConfig` lets a caller pass `NetworkMode` … when the future
  host-network/multi-network scenarios need it").
- `Program.cs`'s existing `dockyarp` resource has no `Docker__HostAddress`; `dockyarp-pp` (the separate
  PROXY-protocol instance) is unrelated and untouched.
- `WithContainerRuntimeArgs(string[])` is the existing mechanism this AppHost already uses (see
  `BackendSpec.ToRuntimeArgs()` /`Program.cs:128`) to pass extra `docker run` flags to a DCP-managed container.

## Goals / Non-Goals

**Goals:**
- Prove `Docker:HostAddress` reaches a real host-network backend end-to-end.
- Prove `Docker:ProxyNetworks` auto-detection genuinely excludes a backend on an unreachable network end-to-end.
- Both scenarios must be robust in CI (GitHub Actions Linux runner), not just Docker Desktop.

**Non-Goals:**
- The unset-`Docker:HostAddress` skip criterion — already covered by `add-host-network-mode`'s unit test; not
  re-derived as a second live proxy instance (the original stub's own noted caveat: `HostAddress` is global to
  one proxy instance, so a live "unset" run would need a second dedicated instance for no real incremental proof
  over the existing unit test).
- Any change to `Docker:HostAddress`/`Docker:ProxyNetworks` themselves — already implemented, untouched here.
- Re-testing `add-network-self-detection`'s own unit-level behavior — this change only proves the resulting
  live skip, not the detection logic itself.

## Decisions

**Both scenarios share the existing `dockyarp` AppHost resource — no second proxy instance.**

Rationale: covered in Context (`BackendAddressResolver.Resolve` never reads `hostAddress` for a non-host-mode
container). Simpler than a second dedicated resource, and avoids the maintenance cost of a fourth `dockyarp`
image instance in the AppHost (there are already two: `dockyarp`, `dockyarp-pp`).

**`--add-host host.docker.internal:host-gateway` added unconditionally to the `dockyarp` resource, not
Docker-Desktop-conditional.**

Rationale: Docker Desktop (Windows/Mac) already resolves `host.docker.internal` natively; the flag is a harmless
redundant `/etc/hosts` entry there. Native Linux Docker (this project's GitHub Actions CI runner, confirmed via
`fix-e2e-ci-runner-timeout`'s own investigation) does **not** resolve it without this flag (Docker Engine
20.10+'s documented `host-gateway` special value). Adding it unconditionally is simpler than branching on
platform and has no downside on the platforms where it's unnecessary.

**Unreachable-network scenario reuses `DiscoveryTests.UnhealthyBackend_IsExcluded`'s assertion pattern (fall
through to `Routing__DefaultHost`), not a `/api/routes` absence check.**

Rationale: matches this project's own established convention for proving exclusion (confirmed by reading
`DiscoveryTests.cs`) rather than inventing a new assertion style; also stronger than a route-listing check since
it proves the *routing* behavior end-to-end (a request genuinely lands on the default backend), not just that an
admin-API listing omits an entry.

**Both scenarios use the `EchoImage` backend (`dockyarp-e2e-backend:local`, `BACKEND_ID` env), not
`traefik/whoami`.**

Rationale: `NonDcpHarnessTests` used `whoami` because it only needed to prove *presence* in `/api/routes`; these
two scenarios need to prove *which* backend actually served a request (host-network reachability; the
unreachable one must NOT be the one that answers) — `EchoImage`'s `BACKEND_ID`/`id` JSON field is this project's
existing mechanism for that (`EchoId(echo)`, used throughout `DiscoveryTests`/`RoutingTests`). The image is
already built and pushed to the local registry by the Nuke `E2E` target before tests run (same precondition
`BackendCatalog`'s own echo backends already depend on).

**`NonDcpHarness.RunContainerAsync` gets a new optional `env` parameter, not a separate method.**

Rationale: minimal extension of the existing method signature (adds one parameter with a `null` default,
non-breaking to `NonDcpHarnessTests`'s existing calls) rather than a parallel `RunContainerWithEnvAsync` — no
behavioral difference beyond passing `Env` through to `CreateContainerParameters`.

## Risks / Trade-offs

- [Risk] The host-network backend's chosen port (`18080`, matching the original backlog stub's own suggestion)
  could collide with something else already bound in the Docker daemon's host network namespace. → Mitigation:
  high, unusual port; this is the same residual risk any host-network container always carries, not new here.
- [Risk] `HostConfig.NetworkMode = "host"` has no effect on Windows-container mode (host networking is
  Linux-only) — already a non-issue: this project's E2E suite requires Linux containers throughout (documented
  in `add-nondcp-e2e-harness`'s own design.md, same reasoning applies unchanged here).
- [Risk] A crashed/killed test run leaves the host-network or unreachable-network container/network orphaned. →
  Mitigation: same `NonDcpHarness.DisposeAsync()` best-effort cleanup already proven in `add-nondcp-e2e-harness`,
  no new mitigation needed.

**Correction found during implementation**: Docker Desktop for Windows/Mac does **not** route `--network host`
to the real host by default — the container listens fine (confirmed via `docker logs`/`docker inspect`), and
DockYarp's own route wiring works correctly (confirmed via `DiscoveryReconciler`'s log: the route is created,
no longer skipped), but the actual proxied request 502s because `host.docker.internal` from inside the
Docker Desktop VM cannot reach a host-mode container's port, and `curl http://localhost:<port>/` from the real
Windows host itself also fails to connect. This is a Docker Desktop virtualization limitation, not a DockYarp
bug — root-caused by testing reachability at three levels (backend logs, `dockyarp`'s route table, and a direct
host-side `curl`) rather than assuming the first symptom (502) meant a DockYarp config or discovery defect.
**Fix**: Docker Desktop's "Enable host networking" beta setting (Settings → Resources → Network) makes
`--network host` route to the real host correctly; confirmed live — the test failed with 502s before enabling
it and passed immediately after, with no other change. Native Linux Docker (this project's GitHub Actions CI
runner) has no such limitation and needs no equivalent setting. `docs/testing.md`'s coverage-map entry for
`HostNetworkModeTests` documents this prerequisite so a future contributor on Windows/Mac doesn't mistake it
for a real regression.
