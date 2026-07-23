## 1. Aspire harness scaffolding (AG-DEP)

- [x] 1.1 Add Aspire packages to `Directory.Packages.props` (`Aspire.Hosting.Testing`, `Aspire.AppHost.Sdk`)
- [x] 1.2 Create `tests/DockYarp.E2E.AppHost` (`Aspire.AppHost.Sdk`, net10) and add it to `DockYarp.slnx`
- [x] 1.3 Create `tests/DockYarp.E2E.Backend` (echo app, SDK container image) and add it to `DockYarp.slnx`
- [x] 1.4 Create `tests/DockYarp.E2E.Tests` (NUnit + `Aspire.Hosting.Testing`) and add it to `DockYarp.slnx`

## 2. Distributed system description (AG-DD / AG-RP)

- [x] 2.1 AppHost: DockYarp container (`dockyarp:local`), socket mount, env (`Docker__Enabled`,
      `AdminApi__ApiKey`, `Routing__DefaultHost`), `/metrics` health check, http endpoint
- [x] 2.2 AppHost: `traefik/whoami` and custom echo backends carrying DockYarp labels via runtime args
- [x] 2.3 AppHost: backends coexist under one proxy instance (single Aspire network; validated at runtime)

## 3. NUnit harness + scenarios (AG-DD / AG-RP / AG-AA)

- [x] 3.1 `[SetUpFixture]` boots the AppHost once, waits for DockYarp healthy, exposes the proxy HTTP client
- [x] 3.2 Route-polling helpers (discovery is asynchronous) shared by scenarios
- [x] 3.3 HTTP scenarios (all `[Category("EndToEnd")]`): basic discovery, multi-host, path rewrite, multi-port,
      priority, default host, Basic Auth, proxy tuning (max body + timeout), health-aware exclusion, forwarded
      headers, admin API (routes/health/auth). Custom-error-pages and static-config variants need a different
      proxy-level configuration than the shared default-host instance and are deferred to a follow-up.

## 4. Nuke wiring (AG-DEP)

- [x] 4.1 `Test` excludes `TestCategory=EndToEnd` (default build stays Docker-free and green)
- [x] 4.2 `E2E` builds `dockyarp:local` + the echo image, then runs `TestCategory=EndToEnd`
- [x] 4.3 New `Release` target depends on `Test`, `E2E`, and the image (release validation runs e2e)
- [x] 4.4 Move the Compose smoke test from `scripts/e2e-compose.sh` into an opt-in `Smoke` Nuke target

## 5. Docs, build & validation

- [x] 5.1 Update `docs/deployment.md` (Nuke targets) and `docs/architecture.md` (HTTP e2e now covered)
- [x] 5.2 `./build.ps1 Test` green with the e2e suite present but excluded (no Docker)
- [x] 5.3 `openspec validate add-e2e-aspire --strict`
- [ ] 5.4 Runtime validation of `./build.ps1 E2E` — deferred to a Docker-capable session (authored, not yet run)
