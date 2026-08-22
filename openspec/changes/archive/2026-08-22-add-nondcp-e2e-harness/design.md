## Context

See `proposal.md` and `openspec/backlog/items/add-nondcp-e2e-harness.md` (the "Findings (2026-08-05)" sections
of `e2e-host-network-mode.md`/`e2e-multi-network.md` explain the DCP blocker in full — not re-derived here).

Current code, read directly:
- `AspireAppHostFixture.cs` is the suite's one `[SetUpFixture]`, in namespace `DockYarp.E2E.Tests`. NUnit
  allows only **one** `[SetUpFixture]` per namespace — a second one in the same namespace is not an option
  (nesting into a sub-namespace would only scope it to test classes placed in that sub-namespace, which isn't
  how this suite's classes are organized today).
- `DockerContainerSource.cs:186-192` resolves its `DockerClientConfiguration` from `options.DockerEndpoint`
  when set, otherwise `new DockerClientConfiguration()` — the platform default. Aspire/DCP itself talks to the
  local daemon via its own default resolution in the same process environment. A harness using the same
  unconfigured default therefore targets the same daemon with no extra wiring.
- `Docker:HostAddress` and `Docker:ProxyNetworks` (`DockerDiscoveryOptions.cs`) already exist and are already
  used by `DockYarp.Docker`'s address-selection logic — this change adds no product code, only test
  infrastructure to exercise them.

## Goals / Non-Goals

**Goals:**
- A reusable helper any E2E test class can use to create/track/tear down Docker resources (networks,
  containers) outside DCP's management, targeting the same daemon Aspire uses.
- Prove the harness actually works: discovery genuinely sees a container it created, cleanup genuinely removes
  what it created (including on a failed run).

**Non-Goals:**
- Implementing the `e2e-host-network-mode` or `e2e-multi-network` scenarios themselves — those are separate,
  already-stubbed backlog items with their own specific acceptance criteria (host-network reachability via
  `Docker:HostAddress`; unreachable-network exclusion via `Docker:ProxyNetworks`). This change proves the
  harness works with a narrower smoke check, not those scenarios' actual assertions — avoids scope creep into
  work two other backlog items already own.
- Any change to `Docker:HostAddress`/`Docker:ProxyNetworks` themselves — already implemented, untouched here.

**Correction found during implementation**: discovery visibility itself is network-gated, not just reachability
of the resulting route. `DiscoveryReconciler` drops a container from `/api/routes` entirely ("no reachable
network address; not routed") unless it shares a network with the proxy's own auto-detected reachable set
(`Docker:ProxyNetworks` unset) — confirmed by reading `DiscoveryReconciler`'s log output during a real run of
the smoke test below, after it first failed with the harness container isolated on Docker's default bridge.
The smoke test therefore connects its container to Aspire/DCP's own per-session network
(`NonDcpHarness.ConnectToAspireSessionNetworkAsync`) after creating it, purely so discovery can compute a route
— this is still proving the harness's discovery contract, not the reachability/exclusion behavior
`e2e-host-network-mode`/`e2e-multi-network` themselves own (those scenarios exercise the opposite: a container
that must stay off that network, or on `host` networking).

## Decisions

**Docker.DotNet, not `System.Diagnostics.Process` + the `docker` CLI — corrected from the backlog stub's own
initial sketch.**

Rationale: `Docker.DotNet` is already a project dependency (`DockYarp.Docker` itself uses it), and its default
endpoint resolution naturally targets the same daemon Aspire/DCP resolves in the same process — the stub's own
sketch called "must target the same Docker daemon Aspire uses" a hard requirement, and CLI shelling would
depend on the ambient `docker` CLI's own context/`DOCKER_HOST` resolution, which is not guaranteed to agree
with .NET's `DockerClientConfiguration()` default in every environment. Confirmed via `dotnet-inspect` that
`INetworkOperations.CreateNetworkAsync`/`DeleteNetworkAsync` and `IContainerOperations.CreateContainerAsync`/
`StartContainerAsync`/`RemoveContainerAsync`, plus `HostConfig.NetworkMode` (a plain string — `"host"` or a
network name, matching `docker run --network`), cover everything the harness needs.

**A plain reusable helper class, not a second `[SetUpFixture]`.**

Rationale: covered in Context — NUnit's one-`[SetUpFixture]`-per-namespace rule rules out a second top-level
fixture in `DockYarp.E2E.Tests`. Each consuming test class instead calls the helper from its own
`[OneTimeSetUp]`/`[OneTimeTearDown]`, tracking exactly the resources *that class* created. This is also more
robust than a suite-wide fixture: a resource-creation failure in one test class's setup doesn't leak into or
block unrelated test classes, and cleanup is naturally scoped.

**Validated by a narrow, explicitly-named smoke test — not `HostNetworkModeTests`/`MultiNetworkTests`.**

Rationale: covered in Non-Goals. The smoke test proves "create outside DCP → DockYarp discovers it → cleanup
removes it" — the harness's own contract — without asserting the specific reachability/exclusion behaviors
`e2e-host-network-mode`/`e2e-multi-network` own. Naming it distinctly (not reusing those two items' expected
class names) keeps the boundary unambiguous for whoever picks up those items next.

## Risks / Trade-offs

- [Risk] A crashed/killed test run leaves orphaned containers/networks behind (the `[OneTimeTearDown]` never
  runs). → Mitigation: deterministic, recognizable naming (a shared prefix) so a human can find and clean them
  up manually; this is the same residual risk `AspireAppHostFixture`/DCP itself already carries for its own
  containers, not a new category of risk.
- [Risk] `HostConfig.NetworkMode = "host"` has no effect on Windows containers/Docker Desktop's Windows
  containers mode (host networking is a Linux-only Docker feature) — but this project's E2E suite already
  requires Linux containers throughout (the AppHost images are `dotnet/sdk`/`aspnet` Linux images), so this
  isn't a new constraint the harness introduces.
