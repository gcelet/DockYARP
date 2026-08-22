## 1. Harness (AG-DEP)

- [x] 1.1 `tests/DockYarp.E2E.Tests/DockYarp.E2E.Tests.csproj`: added a `Docker.DotNet` `PackageReference`
      (version already centralized in `Directory.Packages.props`).
- [x] 1.2 New `tests/DockYarp.E2E.Tests/NonDcpHarness.cs`: an `IAsyncDisposable` helper (not a second
      `[SetUpFixture]` — see design.md) wrapping a `DockerClient` built from `new DockerClientConfiguration()`
      (no explicit endpoint — matches the same-daemon-as-Aspire default). Methods:
      - `PullImageIfMissingAsync(string image, CancellationToken)` — `Images.CreateImageAsync` (pull), guarded
        by `Images.ListImagesAsync` with a reference filter so a repeated pull of an already-cached image
        (e.g. `traefik/whoami`, already pulled by the DCP-managed part of the same suite) is a fast no-op.
      - `CreateNetworkAsync(string name, CancellationToken)` — `Networks.CreateNetworkAsync`; tracks the
        created network id internally. Needed by `e2e-multi-network` later (an unreachable-network backend);
        included now (not deferred) since the backlog stub's own acceptance criteria name it explicitly, not a
        speculative addition.
      - `RunContainerAsync(string image, IDictionary<string,string> labels, HostConfig? hostConfig, CancellationToken)` —
        `Containers.CreateContainerAsync` + `StartContainerAsync`; tracks the created container id internally.
        `hostConfig` lets a caller pass `NetworkMode` (`"host"`, or a network name/id from `CreateNetworkAsync`)
        when the future host-network/multi-network scenarios need it; `null` uses Docker's default bridge.
      - `DisposeAsync()` — best-effort cleanup: `RemoveContainerAsync(id, new ContainerRemoveParameters { Force
        = true })` for every tracked container id, then `DeleteNetworkAsync(id)` for every tracked network id
        (containers first — a network with an attached container can't be removed), catching `DockerApiException`
        per removal (one failed removal must not block the others) with a comment justifying the catch
        (best-effort cleanup, matches the backlog stub's own "idempotent, best-effort" requirement).
- [x] 1.3 `dotnet build DockYarp.slnx` — 0 warnings/errors.

## 2. Smoke validation (AG-DEP)

- [x] 2.1 New `tests/DockYarp.E2E.Tests/NonDcpHarnessTests.cs` (`[Category("EndToEnd")]`, distinct name from the
      future `e2e-host-network-mode`/`e2e-multi-network` test classes — see design.md's scope boundary): its own
      `[OneTimeSetUp]`/`[OneTimeTearDown]` create/dispose a `NonDcpHarness` instance. Two tests:
      - `ContainerCreatedOutsideDcpIsDiscovered` — starts a `traefik/whoami` container outside DCP with
        `VIRTUAL_HOST=nondcp-harness.local`/`VIRTUAL_PORT=80` labels, polls `GET /api/routes` (mirroring
        `AdminApiTests.Routes_ReflectDiscoveredContainers`'s `PollJsonAsync`/`ContainsHost` pattern) until the
        host appears, asserts it does. Proves the harness's container-creation contract (create outside DCP →
        DockYarp discovers it via the existing `dockerproxy` `CONTAINERS=1` listing) — does **not** assert
        reachability/routing through the proxy, out of scope here (see design.md's Non-Goals).
      - `NetworkIsCreatedAndRemoved` — calls `CreateNetworkAsync`, confirms it exists via
        `Networks.InspectNetworkAsync`, then (within the test, not waiting for teardown) removes it directly
        and confirms `InspectNetworkAsync` now throws/404s — proves the network half of the harness works in
        isolation, since the container-discovery test above doesn't exercise it.
- [x] 2.2 Confirm cleanup for real: after the test class's `[OneTimeTearDown]` runs, list containers via
      `docker ps -a` (or the equivalent Docker.DotNet call in a throwaway check) and confirm the harness
      container is gone — not just trust that `RemoveContainerAsync` was called.

## 3. Docs (AG-DOC)

- [x] 3.1 `docs/testing.md`: the "Deliberately not covered by e2e" entry for
      `e2e-host-network-mode`/`e2e-multi-network` currently says both are "blocked under Aspire/DCP" with a
      pointer to `add-nondcp-e2e-harness`. Once this harness exists, update the wording to reflect that the
      blocker is resolved and the two scenarios are now buildable on top of it (still not covered *yet*, since
      writing them is explicitly out of this change's scope) — read the current exact wording first, don't
      assume its shape.

## 4. Final validation (AG-DEP)

- [x] 4.1 `dotnet build DockYarp.slnx` — 0 warnings, 0 errors.
- [x] 4.2 Run the real E2E suite (`./build.ps1 E2E` or `dotnet test ... --filter TestCategory=EndToEnd`) — all
      existing scenarios still green (no regression from the new package reference / test file), and the new
      `NonDcpHarnessTests` scenario passes for real, not just compiles.
- [x] 4.3 Real orphan check: after a full suite run, confirm via `docker ps -a` / `docker network ls` that
      nothing named after this harness's containers/networks is left running — proves cleanup actually happens
      outside of just the one targeted test class's own run.
