## Why

`Docker:HostAddress` (host-network routing) and `Docker:ProxyNetworks` (multi-network reachability, now
auto-detected when unset) are both implemented and unit-tested, but neither has ever been proven end-to-end
against a real Docker daemon: Aspire/DCP's single managed network made both impossible to exercise live. That
blocker is now resolved (`add-nondcp-e2e-harness`, `NonDcpHarness` in `tests/DockYarp.E2E.Tests/`) — this change
is the two scenarios it was built to unblock, batched into one change since they share the same harness and the
same `skip_specs` shape (the original stubs themselves suggested batching).

## What Changes

- New e2e test class(es) in `tests/DockYarp.E2E.Tests/` (distinctly named from `NonDcpHarnessTests`, per
  `add-nondcp-e2e-harness`'s own design.md scope boundary): `HostNetworkModeTests`, `MultiNetworkTests`.
- **Host-network scenario**: a `NonDcpHarness`-created container with `HostConfig.NetworkMode = "host"`, labeled
  `VIRTUAL_HOST`/`VIRTUAL_PORT`. The existing shared `dockyarp` AppHost resource gets
  `Docker__HostAddress=host.docker.internal` added to its environment (confirmed safe to add unconditionally —
  `BackendAddressResolver.Resolve` only consults `hostAddress` when a container is in host-network mode; it has
  no effect on any other backend's address resolution, so no second dedicated proxy instance is needed). Assert
  a request through DockYarp reaches the host-network backend.
- **Unreachable-network scenario**: a `NonDcpHarness`-created network + a container attached only to it (not the
  Aspire session network), labeled `VIRTUAL_HOST`/`VIRTUAL_PORT`. Assert it is excluded from `/api/routes` and a
  warning is logged — this is the same `DiscoveryReconciler` exclusion path discovered live while validating
  `add-nondcp-e2e-harness` ("no reachable network address; not routed").
- `docs/testing.md`'s coverage map: remove the now-fully-covered `Docker:HostAddress`/`Docker:ProxyNetworks`
  live-validation entries (or update them to point at the new tests), since both criteria the map currently
  lists as "not covered by e2e" become covered by this change.
- No `Docker:HostAddress`/`Docker:ProxyNetworks` behavior changes — both are already implemented; this change
  only proves them live.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
(none — pure e2e test-infrastructure/verification, no DockYarp behavior change; `skip_specs: true` is set in
this change's `.openspec.yaml`, matching the backlog item's own note and the precedent set by
`add-nondcp-e2e-harness`)

## Impact

- `tests/DockYarp.E2E.Tests/` — new test class file(s) using `NonDcpHarness`.
- `tests/DockYarp.E2E.AppHost/Program.cs` — add `Docker__HostAddress` to the existing shared `dockyarp`
  resource's environment.
- `docs/testing.md` — update the "Deliberately not covered by e2e" coverage map now that both criteria are
  covered.
- No `src/` changes — `Docker:HostAddress`/`Docker:ProxyNetworks` already exist and are unmodified.
