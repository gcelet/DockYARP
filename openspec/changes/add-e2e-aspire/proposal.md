## Why

DockYarp has strong unit/integration coverage running **in-process** (`WebApplicationFactory`), but its
core value — discovering **real** Docker containers, wiring routes from their labels, and proxying to them
over real container networking — is never exercised end to end. The only runtime check today is a Nuke `E2E`
target that does `docker compose up` and one `curl -H "Host: whoami.local"`; it is a single-scenario smoke
test with no assertions on discovery, routing precedence, auth, path rewriting, or the admin API.

We want a real end-to-end harness that boots a distributed system (DockYarp container + labeled backend
containers) on a live Docker daemon and asserts behaviour through HTTP, driven from NUnit via **.NET Aspire**
(mirroring the `Aspire.Hosting.Testing` pattern already used in the author's other projects). It must be
runnable and gated as part of **release validation**, yet **never** run during the ordinary build/test so the
default developer loop stays Docker-free.

## What Changes

- Add an Aspire-based end-to-end test suite (HTTP scope first) under `tests/`:
  - `DockYarp.E2E.AppHost` — Aspire AppHost describing the distributed system (DockYarp container +
    `traefik/whoami` + a small custom echo backend), wiring DockYarp labels as real Docker labels and
    mounting the Docker socket.
  - `DockYarp.E2E.Backend` — a minimal ASP.NET Core echo app (method/path/host/headers/body-size/port)
    for scenarios `whoami` cannot cover, with a `Dockerfile`.
  - `DockYarp.E2E.Tests` — NUnit harness (`[SetUpFixture]` boots the AppHost once) plus HTTP scenarios
    (discovery, multi-host, path rewrite, multi-port, priority, default host, Basic Auth, proxy tuning,
    health-aware, forwarded headers, custom error pages, admin API). Every test is `[Category("EndToEnd")]`.
- Wire Nuke so end-to-end is **opt-in and release-gated**:
  - `Test` (default chain) **excludes** the `EndToEnd` category — the plain build stays Docker-free and green.
  - `E2E` (opt-in) builds the `dockyarp:local` image, then runs the Aspire suite (`Category=EndToEnd`).
  - `Release` (new) validates a version by depending on the full gate **including** `E2E`.
  - `Smoke` (opt-in) replaces the `scripts/e2e-compose.sh` bash script: the Compose smoke test moves into a
    Nuke target so it is driven by the build, not a shell script, and is likewise excluded by default.
- TLS/ACME/mTLS end-to-end (step-ca) is **out of scope** here and deferred to a follow-up change
  (`add-e2e-tls-acme`); HTTP endpoints are covered now.

## Capabilities

### Modified Capabilities
- `deployment`: adds an Aspire-based end-to-end test suite that is runnable via Nuke, part of release
  validation, and excluded from the default build/test.

## Impact

- **Code**: new `tests/DockYarp.E2E.AppHost`, `tests/DockYarp.E2E.Backend`, `tests/DockYarp.E2E.Tests`;
  `build/Build.cs` (`Test` excludes e2e; new `E2E`, `Release`, `Smoke`); `Directory.Packages.props` (Aspire
  packages); `DockYarp.slnx`; removed `scripts/e2e-compose.sh`; `docs/deployment.md`, `docs/architecture.md`.
- **Runtime prerequisite**: `E2E`/`Release` require a Docker daemon reachable by Aspire's DCP (documented).
  The default `Test`/`Compile` do not.
- **Owning agent**: AG-DEP (with AG-DD / AG-RP for the scenario coverage).
