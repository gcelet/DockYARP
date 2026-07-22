## Context

`DockerContainerSource.ResolveAddress` picks `NetworkSettings.Networks.Values.FirstOrDefault(has IP)`. The
enumeration order of the networks dictionary is not guaranteed, so the chosen IP can vary between
reconciliations, and on a Swarm host the `ingress` network's IP (not routable for direct proxying) can win.
The fix is a deterministic, configurable selection, kept pure so it is testable without a Docker daemon.

## Goals / Non-Goals

**Goals:** honor a configured preferred network; otherwise choose deterministically and skip `ingress`;
fall back to the container name when no address exists. Pure selection logic under unit test.

**Non-Goals (deferred):** `NETWORK_ACCESS=internal` (an access-control concern, not address selection —
belongs with the security capability), host-network mode, IPv6 preference, and per-container network
override labels.

## Decisions

- **Pure `NetworkAddressSelector.Select(networkAddresses, preferredNetwork)`** returning the chosen IP or
  `null`:
  - a non-empty preferred network with a usable IP wins;
  - otherwise the first usable IP by **ordinal network-name order**, skipping `ingress`;
  - `null` when nothing is usable (caller falls back to the container name).
  The `ingress` skip is a constant (Swarm's well-known ingress network); making the skip list configurable
  is deferred until a requirement needs it.
- **`DockerDiscoveryOptions.PreferredNetwork`** (string?, bound from the existing `Docker` config section).
- **`DockerContainerSource`** stores the preferred network and builds a `name → IP` map from
  `NetworkSettings.Networks` for the selector; `ResolveAddress`/`ToContainerInfo` become instance members.

## Risks / Trade-offs

- Ordinal-by-name is arbitrary but **stable**; the real fix for multi-network correctness is setting
  `PreferredNetwork` to the proxy's shared network, which the deterministic fallback complements.
- Skipping `ingress` by name assumes the standard Swarm network name; documented, and overridable later.

## Migration Plan

Additive: one option + a pure helper + rerouting `ResolveAddress` through it. Behavior only changes for
containers on multiple networks (now deterministic / ingress-aware) — single-network containers are
unaffected.

## Open Questions

- Auto-detecting the proxy's own network (à la docker-gen) instead of configuring `PreferredNetwork` —
  deferred; needs runtime validation.
