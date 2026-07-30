# Design — add-host-network-mode

## Context
`DockerContainerSource.ResolveAddress` selects a container IP from its Docker networks. A host-network
container has no such IP, so selection returns nothing and the container is unroutable. `ContainerListResponse`
(the list API) exposes no `HostConfig.NetworkMode`, so host mode must be inferred from the data the list
already returns.

## Decisions

### 1. Detect host mode from the reserved `host` network entry
A host-network container reports a network named `host` in `NetworkSettings.Networks` (with no usable IP).
`host` is a reserved predefined Docker network name that users cannot create, so its presence is a reliable
host-mode signal available from the list API — no extra per-container `inspect` call. Detection is a pure check
on the network-name set.

### 2. `Docker:HostAddress` names how to reach the host
Add `Docker:HostAddress` (string): the address the proxy uses to reach the Docker host — `host.docker.internal`
on Docker Desktop, or the host-gateway/LAN IP on Linux. There is no portable default (the right value depends
on the platform and compose setup), so when it is unset a host-network backend is skipped rather than routed to
a guess.

### 3. Consolidate the address decision in a pure `BackendAddressResolver`
A single pure helper decides the backend host part (not the port), folding in the existing reachability rule:
- **host mode** → `HostAddress` when configured, else empty (skip);
- **otherwise** → the selected network IP; if none, empty when reachability is known (`ProxyNetworks` set) else
  the container name (embedded DNS on a shared network).

`DockerContainerSource` computes the networks once, detects host mode, runs `NetworkAddressSelector` only for
non-host containers, and calls the resolver. `ContainerInfo.IsHostNetwork` is recorded so the mapper can
explain a skip precisely.

### 4. `VIRTUAL_PORT` requirement is already enforced
Port resolution requires `VIRTUAL_PORT` unless the container exposes exactly one port. A host-network container
exposes no ports in the list API, so a missing `VIRTUAL_PORT` already fails port resolution and skips the
container with a clear message — no host-mode-specific port logic is needed.

### 5. Mapper warning specificity
The empty-address skip (from `add-multi-network-attachment`) now distinguishes host mode: a host-network
container with no address warns that `Docker:HostAddress` must be set, versus the generic "no reachable network
address" for the multi-network case.

### 6. Deferred: live reachability validation
Whether `HostAddress` is actually reachable from inside the proxy container is platform-specific
(`host.docker.internal` needs `--add-host host.docker.internal:host-gateway` on Linux). That live check is
deferred to `e2e-host-network-mode`, consistent with how gRPC and multi-network runtime validation were split.

## Risks
- Detecting host mode via the `host` network key misses a host-mode container that reports empty `Networks`
  (uncommon on the list API). Acceptable: such a container was already unroutable; the common representation
  includes the `host` entry.
