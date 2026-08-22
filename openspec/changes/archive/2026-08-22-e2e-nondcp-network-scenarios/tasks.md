## 1. Harness extension (AG-DEP)

- [x] 1.1 `tests/DockYarp.E2E.Tests/NonDcpHarness.cs`: add an `IReadOnlyDictionary<string,string>? env`
      parameter to `RunContainerAsync` (required, not optional — AV1553), threaded into
      `CreateContainerParameters.Env` as `"KEY=VALUE"` strings. `NonDcpHarnessTests`'s existing call updated to
      pass `env: null`. `dotnet build DockYarp.slnx` — 0 warnings/errors.

## 2. AppHost wiring (AG-DD)

- [x] 2.1 `tests/DockYarp.E2E.AppHost/Program.cs`: add `Docker__HostAddress=host.docker.internal` and
      `.WithContainerRuntimeArgs("--add-host", "host.docker.internal:host-gateway")` to the existing shared
      `dockyarp` resource (not `dockyarp-pp`, unrelated) — see design.md's Decisions for why this is safe to add
      unconditionally (no effect on non-host-mode containers) and why the flag is added regardless of platform.
      `dotnet build DockYarp.slnx` — 0 warnings/errors.

## 3. Host-network scenario (AG-DD)

- [x] 3.1 New `tests/DockYarp.E2E.Tests/HostNetworkModeTests.cs` (`[Category("EndToEnd")]`, distinct name from
      `NonDcpHarnessTests` per its own design.md scope boundary): own `[OneTimeSetUp]`/`[OneTimeTearDown]`
      create/dispose a `NonDcpHarness` instance.
- [x] 3.2 `HostNetworkBackend_IsReachedThroughDockYarp` — via `harness.PullImageIfMissingAsync` +
      `RunContainerAsync` with `HostConfig.NetworkMode = "host"`: start the `EchoImage` (`dockyarp-e2e-backend`,
      tag `local`) with `VIRTUAL_HOST=hostnet.local`/`VIRTUAL_PORT=18080` labels and
      `ASPNETCORE_URLS=http://+:18080`/`BACKEND_ID=hostnet` env. Poll `Host: hostnet.local` through `Proxy`
      (`PollUntilSuccessAsync`/`EchoId`) until the response's `id` is `"hostnet"` — proves the request actually
      reached the host-network backend end-to-end, not just that some 200 came back.

## 4. Unreachable-network scenario (AG-DD)

- [x] 4.1 New `tests/DockYarp.E2E.Tests/MultiNetworkTests.cs` (`[Category("EndToEnd")]`, distinct name, own
      `[OneTimeSetUp]`/`[OneTimeTearDown]` `NonDcpHarness` instance).
- [x] 4.2 `UnreachableNetworkBackend_FallsThroughToDefault` — via `harness.CreateNetworkAsync` (a network the
      Aspire session never joins) then `RunContainerAsync` with that network's id as `HostConfig.NetworkMode`
      (the container's *only* network — not also connected to the Aspire session network, unlike
      `NonDcpHarnessTests`'s discovery smoke test): start the `EchoImage` with
      `VIRTUAL_HOST=unreachable.local`/`VIRTUAL_PORT=8080` labels and `ASPNETCORE_URLS=http://+:8080`/
      `BACKEND_ID=unreachable` env. Poll `Host: unreachable.local` through `Proxy`, mirroring
      `DiscoveryTests.UnhealthyBackend_IsExcluded`'s exact assertion pattern: assert the response's `id` is
      `"default"` (`BackendCatalog.DefaultHost`'s backend), proving the unreachable backend is excluded and the
      request falls through — not that some route-listing merely omits it.

## 5. Docs (AG-DOC)

- [x] 5.1 `docs/testing.md`: read the current "Deliberately not covered by e2e" wording for
      `Docker:HostAddress`/`Docker:ProxyNetworks` live validation (updated most recently by
      `add-nondcp-e2e-harness`) and update it to reflect both are now covered by
      `HostNetworkModeTests`/`MultiNetworkTests` — don't assume its exact current shape, re-read first.

## 6. Final validation (AG-DEP)

- [x] 6.1 `dotnet build DockYarp.slnx` — 0 warnings, 0 errors.
- [x] 6.2 Run the real E2E suite (`./build.ps1 E2E` or `dotnet test ... --filter TestCategory=EndToEnd`) — all
      existing scenarios still green (no regression), and both new scenarios pass for real.
- [x] 6.3 Real orphan check: after a full suite run, confirm via `docker ps -a` / `docker network ls` that
      nothing named after this change's containers/networks is left running.
