## Why
nginx-proxy exposes two IPv6 knobs: `ENABLE_IPV6` (listen on IPv6) and `PREFER_IPV6_NETWORK` (forward to a backend's
IPv6 address when it has both families). The parity matrix marks IPv6 ⛔.

On inspection, the **listening** half is already satisfied: DockYarp binds its edges with Kestrel `ListenAnyIP`, which
listens on `[::]` (IPv6Any) in dual-stack mode, so IPv6 clients are already served (IPv4 via v4-mapped). The real gap
is **backend address family selection**: `DockerContainerSource` reads only each network's IPv4 address
(`EndpointSettings.IPAddress`), so an IPv6-only backend has no forwardable address and a dual-stack backend is always
reached over IPv4.

## What Changes
- Capture **both** address families per network during discovery (`IPAddress` and `GlobalIPv6Address`).
- Add `Docker:PreferIpv6` (from nginx `PREFER_IPV6_NETWORK`, default `false`). When enabled, the network address
  selector forwards to the chosen network's **IPv6** address; otherwise IPv4. Exactly **one** family is selected (no
  duplicate endpoints), falling back to the other family when the preferred one is absent on that network — so an
  IPv6-only backend is now routable regardless of the toggle.
- The network-selection rules (preferred network, `ingress` skip, reachable-set restriction, deterministic ordering)
  are unchanged — only the address **within** the chosen network gains a family preference.
- **Document** that IPv6 listening is already on by default (dual-stack `ListenAnyIP`), unlike nginx's opt-in
  `ENABLE_IPV6`; no new listener toggle is added.

## Capabilities
### Modified Capabilities
- `docker-discovery`: network address selection can prefer a backend's IPv6 address, and captures both families.

## Impact
- **Code (Docker discovery only — no Core/App model change; `ContainerInfo.Address` stays the resolved address)**:
  a new `NetworkAddresses(Ipv4, Ipv6)` value + `AddressFamilyPreference` enum; `NetworkAddressSelector.Select` and
  `BackendAddressResolver` take the per-network pair; `DockerContainerSource.BuildNetworks` reads both families;
  `DockerDiscoveryOptions.PreferIpv6`.
- **Tests**: `DockYarp.Docker.Tests` — selector picks IPv6 when preferred, falls back to IPv4 when no IPv6, keeps IPv4
  by default; an IPv6-only network is routable; host-mode/ingress/reachable rules unchanged.
- **Docs (user-facing — new app-config key)**: docs site `configuration.md` (`Docker:PreferIpv6`) + a `features.md`
  note that IPv6 listening is dual-stack by default and backends can be reached over IPv6.
- **Out of scope / deferred**: a live IPv6-network e2e (needs an IPv6-enabled Docker network) — the selection logic is
  fully unit-tested and the listener is already dual-stack. Owning agent: AG-DD (with AG-DEP for the listener note).
