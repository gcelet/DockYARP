## Why

`e2e-host-network-mode` and `e2e-multi-network` are blocked under Aspire/DCP: DCP attaches every container to
one managed network, so a `--network host` container can't start (Docker forbids combining host networking
with DCP's own network attachment) and a backend can't be made genuinely *unreachable* from the proxy. Both
underlying DockYarp features already exist and are implemented (`Docker:HostAddress`, `Docker:ProxyNetworks`
— confirmed by reading `DockerDiscoveryOptions.cs`) — what's missing is a test harness able to create
containers/networks *outside* DCP's management so these features can actually be exercised end-to-end.

## What Changes

- A new NUnit `[SetUpFixture]` (or an extension alongside `AspireAppHostFixture`) that manages extra
  containers/networks directly via **Docker.DotNet** (already a project dependency, used by
  `DockYarp.Docker` itself) — not raw `docker` CLI shelling as first sketched, since `Docker.DotNet`'s default
  endpoint resolution naturally targets the same daemon Aspire/DCP uses in the same test process (no
  CLI-context/`DOCKER_HOST` mismatch risk).
- `[OneTimeSetUp]`: create the extra Docker networks and start the extra backend containers (deterministic
  names, DockYarp discovery labels) needed by the two parked scenarios.
- `[OneTimeTearDown]`: remove them, best-effort and idempotent, robust to a failed run leaving nothing behind.
- This item ships the harness only — `e2e-host-network-mode` and `e2e-multi-network` themselves (the actual
  test scenarios that use it) are separate, already-stubbed backlog items that build on top of it once this
  lands.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
(none — this is test infrastructure with no DockYarp behavior change; `skip_specs: true` is set in this
change's `.openspec.yaml`, matching the pre-existing backlog item's own note: "e2e-* items = single commit, no
OpenSpec archive; this harness item... is likewise a single-commit change")

## Impact

- `tests/DockYarp.E2E.Tests/` — new fixture file(s); `DockYarp.E2E.Tests.csproj` needs a `Docker.DotNet`
  `PackageReference` (version centralized in `Directory.Packages.props`, already pinned there).
- `docs/testing.md` — the E2E coverage map's "Deliberately not covered by e2e" list currently explains
  `e2e-host-network-mode`/`e2e-multi-network` as "blocked under Aspire/DCP" with a pointer to this backlog
  item; once the harness exists (even before the two scenarios themselves are written), that framing is
  slightly stale and worth a one-line update once this ships.
- No `src/` changes — `Docker:HostAddress`/`Docker:ProxyNetworks` already exist and are unmodified.
