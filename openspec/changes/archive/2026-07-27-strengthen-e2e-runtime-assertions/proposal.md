## Why
Two behaviors from recent changes are only fully observable against the **real runtime**: the `Server` header
suppression (Kestrel `AddServerHeader = false`, invisible in the in-process TestServer) and the HTTP→HTTPS
redirect **308** status (asserted at the middleware level, but not yet end-to-end). Rather than add artificial
tests, strengthen the existing e2e scenarios that already exercise these flows.

## What Changes
- `DiscoveryTests.Whoami_IsDiscoveredAndProxied`: also assert the proxied response carries no `Server` header.
- `TlsTests.HttpRequest_RedirectsToHttps`: also assert the redirect status is exactly 308.

## Capabilities
### New Capabilities
<!-- None. -->
### Modified Capabilities
- `deployment`: the end-to-end suite additionally asserts `Server`-header suppression and the 308 redirect
  status against the real runtime.

## Impact
- **Code**: tests only — `tests/DockYarp.E2E.Tests/DiscoveryTests.cs`, `tests/DockYarp.E2E.Tests/TlsTests.cs`.
- **Deferred**: `Proxy`-header strip in e2e (already covered significantly by an in-process integration test);
  raw-IPv4 host (unit-covered) — neither warrants e2e per the test pyramid.
- **Owning agent**: AG-DEP.
- **Runtime**: these run under the opt-in `E2E` target (Docker required); authored and compile-validated now,
  executed at the next Docker/WSL `E2E` run.
