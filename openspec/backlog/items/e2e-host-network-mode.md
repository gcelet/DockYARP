---
id: e2e-host-network-mode
capability: docker-discovery
agent: AG-DD
tier: B-runtime
priority: low
status: backlog
nginx-proxy: host network mode backends (runtime validation)
provenance: deferred from add-host-network-mode, 2026-07-30
---

## Why
`add-host-network-mode` routes host-network backends to a configured `Docker:HostAddress` and unit-tested the
detection + address logic, but did not validate that the configured host address is actually reachable from
inside the proxy container. That reachability is platform-specific and needs a live check.

## nginx-proxy behavior
- Host-network backends are supported; they must set `VIRTUAL_PORT`, and nginx targets the host address on that
  port (`test_host-network-mode`).

## DockYarp today
- `Docker:HostAddress` + host-mode detection are implemented and unit-tested (`add-host-network-mode`). No live
  round-trip proves the proxy can reach a host-network backend.

## Proposed change (sketch)
- Add an Aspire e2e scenario with a backend published on the host network, labeled `VIRTUAL_HOST` +
  `VIRTUAL_PORT`, and assert a request through DockYarp reaches it.
- Exercise the platform wiring: `host.docker.internal` on Docker Desktop vs Linux
  (`--add-host host.docker.internal:host-gateway` or the host-gateway IP).
- Assert the skip-with-warning path when `Docker:HostAddress` is unset.

## Acceptance criteria (→ scenarios)
- **WHEN** a host-network backend is labeled and `Docker:HostAddress` is set **THEN** a request through DockYarp
  reaches it end to end.
- **WHEN** `Docker:HostAddress` is unset **THEN** the host-network backend is not routed and a warning is logged.

## Notes / risks / references
- Requires a Docker-capable session; validate `host.docker.internal` availability per platform.
- Sibling (done): `add-host-network-mode` (detection + address logic).
- Related: `e2e-multi-network` (also needs live network validation) — consider batching.

## Findings (2026-08-05 — BLOCKED under the current Aspire/DCP harness)
Investigated while running the e2e batch. **This is not achievable with the existing Aspire AppHost.** Aspire
models a single DCP-managed network and attaches every container to it. A `--network host` container (needed to
trigger host-mode detection) **cannot** be launched via `WithContainerRuntimeArgs("--network","host")`: Docker
forbids combining host networking with another `--network`, and DCP always adds its own — the container fails to
start. Aspire exposes no host-network API. Options for a future attempt:
- Run the host-network backend **outside** DCP (a plain `docker run --network host` the test fixture manages
  directly, then point `Docker:HostAddress=host.docker.internal` + `--add-host host.docker.internal:host-gateway`
  on the proxy). This is a harness change, not a BackendCatalog entry.
- Or accept that host-mode **detection + address logic is unit-tested** (`add-host-network-mode`) and mark this
  runtime-only-validatable-out-of-DCP; skip the e2e.
Decide the harness approach before spending another `./build.sh E2E`.

### Concrete non-DCP harness sketch (for the retry)
- In a NUnit `[OneTimeSetUp]` (a new fixture, e.g. `HostNetworkFixture`, or an extension of `AspireAppHostFixture`),
  start the backend yourself: `docker run -d --network host --label VIRTUAL_HOST=hostnet.local
  --label VIRTUAL_PORT=<free host port> --name dockyarp-e2e-hostnet <EchoImage>` (pick a free high port, e.g.
  18080; `ASPNETCORE_URLS=http://+:18080`). Tear it down in `[OneTimeTearDown]` (`docker rm -f`).
- The `dockerproxy` socket-proxy lists ALL containers (CONTAINERS=1), so DockYarp discovers it even though it is
  outside the DCP network. It detects host-mode and, with `Docker__HostAddress=host.docker.internal` set on the
  `dockyarp` container (+ `.WithContainerRuntimeArgs("--add-host","host.docker.internal:host-gateway")`), forwards
  to `host.docker.internal:18080`.
- Assert a request `Host: hostnet.local` through DockYarp reaches the echo backend. Caveat: setting
  `HostAddress` is global, so the unset-skip criterion needs a **separate** run/config (or rely on its unit test).
- Risk: the out-of-DCP container must share the same Docker daemon Aspire uses; port must be free on the host/VM.
