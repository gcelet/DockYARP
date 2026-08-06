---
id: add-nondcp-e2e-harness
capability: deployment
agent: AG-DEP
tier: B-runtime
priority: low
status: backlog
nginx-proxy: (internal — e2e infrastructure)
provenance: 2026-08-06 (unblocks the DCP-parked e2e; user idea)
---

## Why
`e2e-host-network-mode` and `e2e-multi-network` are **blocked under Aspire/DCP**: DCP attaches every container to a
single managed network, so a `--network host` container cannot start (Docker forbids combining host networking with
another `--network`, and DCP adds its own) and a backend cannot be made *unreachable* from the proxy. A non-DCP
harness that manages the extra Docker networks/containers **directly** (via the `docker` CLI) sidesteps this. (See
the "Findings (2026-08-05)" sections of both parked e2e stubs.)

## Current state
- The Aspire AppHost (DCP) owns all containers on one DCP network. The `dockerproxy` socket-proxy lists **all**
  containers (`CONTAINERS=1`), so DockYarp already discovers containers created outside DCP.

## Proposed change (sketch)
- A NUnit fixture (e.g. `NonDcpDockerFixture`, or an extension of `AspireAppHostFixture`) that, via
  `System.Diagnostics.Process` running the `docker` CLI:
  - `[OneTimeSetUp]`: create the networks needed and `docker run -d` the extra backends **outside DCP**
    (deterministic names, labels = the DockYarp discovery labels). For host-network: `docker run --network host …`;
    for multi-network: `docker network create <net>` + a backend attached **only** to it (unreachable from the
    proxy on the DCP network).
  - `[OneTimeTearDown]`: `docker rm -f` the containers + `docker network rm` the networks (idempotent, best-effort).
  - Must target the **same Docker daemon** Aspire uses; robust cleanup even on test failure.
- Then `e2e-host-network-mode` (reach a host-network backend via `Docker__HostAddress=host.docker.internal` +
  `--add-host host.docker.internal:host-gateway` on the proxy) and `e2e-multi-network` (assert the backend on the
  unshared network is skipped with a warning) build on this fixture.

## Acceptance criteria (→ scenarios)
- **WHEN** the harness runs **THEN** the extra networks/containers are created outside DCP, discovered by DockYarp,
  and fully cleaned up afterwards (no leaks on failure).
- **WHEN** used by `e2e-host-network-mode`/`e2e-multi-network` **THEN** those scenarios become runnable.

## Notes / risks / references
- **WSL-only** (needs Docker); moderate effort. This is the enabler `e2e-host-network-mode` + `e2e-multi-network`
  depend on. `e2e-*` items = single commit, no OpenSpec archive; this harness item, being test infrastructure with
  no spec requirement, is likewise a single-commit change (announce it).
