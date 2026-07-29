# Design — add-multi-network-attachment

## Context
`NetworkAddressSelector.Select` (pure, unit-tested) picks a container IP: the preferred network if attached,
else the first non-`ingress` network by ordinal name, else `null` (the caller falls back to the container
name). It is not reachability-aware: the deterministic fallback can pick a network the proxy is not attached
to, and a backend whose only networks are unreachable still yields an address (IP or name) that cannot be
reached.

## Decisions

### 1. Model the proxy's reachability as a configured network set
Add `Docker:ProxyNetworks` (`IList<string>`, default empty): the networks the proxy is attached to. This keeps
the selection logic **pure and unit-testable** — no live self-inspection of the proxy container. Runtime
auto-detection of the proxy's own memberships is deferred (see below). Empty ⇒ reachability unknown ⇒ today's
behavior, so the change is opt-in and backward compatible.

### 2. Reachability-aware selection
`Select` gains a required `proxyNetworks` parameter:
- An explicit **preferred network** with an IP still wins (operator intent; it is the shared network by
  definition of the option).
- Otherwise, among usable non-`ingress` networks: when `proxyNetworks` is non-empty, keep only those the proxy
  shares (reachable) before the deterministic ordinal pick; when empty, keep all (unchanged).
- If nothing remains, return `null`.

This makes "backend on several networks, proxy shares one" select the shared address, and "backend only on an
unreachable network" return `null`.

### 3. Skip unreachable backends instead of routing them
`DockerContainerSource.ResolveAddress`: when `Select` returns `null`, fall back to the container **name** only
when reachability is unknown (`ProxyNetworks` empty) — Docker embedded DNS resolves the name on a shared
network. When reachability **is** known and no shared network exists, return an empty address. `ContainerMapper`
then skips any container with an empty address, emitting `no reachable network address; not routed`, instead of
building a broken `scheme://:port` endpoint. The skip lives in the mapper alongside the other skip/warn
decisions (health, invalid labels).

### 4. Deferred: runtime auto-detection + live validation
Detecting the proxy's own networks from inside its container (so `ProxyNetworks` need not be configured) and
validating the live skip path require a Docker-capable session. Deferred to a runtime/e2e follow-up, matching
how gRPC round-trip validation was split from its config change.

## Risks
- If `ProxyNetworks` is configured but incomplete, a genuinely reachable backend could be skipped. Mitigated by
  the option being opt-in and by the explicit warning naming the container, so misconfiguration is visible.
