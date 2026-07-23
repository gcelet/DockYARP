## Context

The proxy's discovery + routing behaviour is only real against a live Docker daemon: containers must exist,
carry labels, share a reachable network, and be discovered asynchronously. In-process tests use a
`FakeContainerSource`, so they never exercise `Docker.DotNet`, real container IPs, or the label→route→proxy
path as a whole. Aspire (`Aspire.Hosting.Testing`, 13.1.0, net10) lets an NUnit process describe and boot a
distributed system on the real Docker runtime (via DCP) and then talk to it over HTTP — the same pattern the
author already uses elsewhere (`ProtectedNumbers.Tests.EndToEnd`).

## Goals / Non-Goals

**Goals:** a real end-to-end HTTP harness (DockYarp container discovering labeled backend containers);
NUnit assertions on the main discovery/routing features; Nuke wiring so e2e is opt-in and release-gated but
absent from the default build.

**Non-Goals (this change):** TLS/ACME/mTLS end to end (needs a CA — deferred to `add-e2e-tls-acme`); Swarm;
PROXY protocol; replacing the existing in-process integration tests.

## Decisions

- **Real Docker via Aspire.** DockYarp runs as a container (`AddContainer("dockyarp", "dockyarp",
  "local")`, the image pre-built by Nuke's `E2E`) mounting the daemon socket read-only; backends run as
  sibling containers on a shared network. Rejected: simulating the Docker socket — high effort and it would
  only re-test the fake source, not the real `Docker.DotNet` path.
- **Two backend kinds.** `traefik/whoami` covers method/host/path/header echo. A small custom
  `DockYarp.E2E.Backend` (ASP.NET Core) covers what whoami cannot: large-body handling
  (`DOCKYARP_MAX_BODY_SIZE`), a slow endpoint (`DOCKYARP_PROXY_TIMEOUT`), multi-port
  (`VIRTUAL_HOST_MULTIPORTS`), and a client-cert subject echo (reserved for the TLS follow-up).
- **Labels as real Docker labels.** Backends carry DockYarp labels via container runtime args
  (`--label VIRTUAL_HOST=...`) so discovery reads them exactly as in production.
- **Shared network.** All containers join one Docker network and DockYarp is pointed at it
  (`Docker__PreferredNetwork`) so discovered backend IPs are reachable from the proxy container.
- **Readiness.** DockYarp exposes `/metrics` unauthenticated; the harness waits for the resource to be
  healthy via a health check on `/metrics` before asserting. Because discovery is asynchronous, each scenario
  polls the route until it is live (bounded), like the in-process proxy tests.
- **Category gate.** Every e2e test is `[Category("EndToEnd")]`. Nuke's `Test` filters `TestCategory!=EndToEnd`
  (default stays Docker-free); `E2E` runs `TestCategory=EndToEnd` after building `dockyarp:local`;
  `Release` depends on `Test`, `E2E`, and the image so a release runs the full gate while a plain build does
  not.

## Risks / Trade-offs

- **Aspire container label/network API** is the top runtime unknown (exact `WithContainerRuntimeArgs`
  `--label`/`--network` surface and the matching `Docker__PreferredNetwork` value). The harness is authored
  now; it is validated in a Docker-capable session, with Aspire's default network as a fallback.
- **Docker reachability.** `E2E`/`Release` need a daemon reachable by DCP; on this machine Docker lives in
  WSL, so `DOCKER_HOST`/context must point at it. Documented; the default build needs no daemon.
- **Socket path** for the bind mount depends on the daemon host (`/var/run/docker.sock`).

## Migration Plan

Purely additive: three new test-tree projects and new/adjusted Nuke targets. The old compose-based `E2E`
smoke is superseded by the Aspire suite; its target body is replaced. No production code changes.

## Open Questions

- Exact Aspire 13.1 network wiring (shared user network vs. default) — resolved at runtime validation.
- Whether the custom echo backend is built via `AddDockerfile` (Aspire builds the image) or pre-published;
  `AddDockerfile` is the current intent.
