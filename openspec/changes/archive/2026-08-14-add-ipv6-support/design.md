# Design — add-ipv6-support

## Finding: the listener is already dual-stack
`KestrelTlsConfigurator` binds both edges with `serverOptions.ListenAnyIP(port, …)`. `ListenAnyIP` listens on
`IPAddress.IPv6Any` (`[::]`) in dual-stack mode, so IPv6 clients are already served today (IPv4 arrives v4-mapped).
nginx's `ENABLE_IPV6` is opt-in; DockYarp is dual-stack by default. **No listener change** — this is documented, not
coded. The parity gap is therefore only the backend address family.

## The change: per-network address families
Today `DockerContainerSource.BuildNetworks` collapses each network to its IPv4 address:
```csharp
map.ToDictionary(pair => pair.Key, pair => pair.Value?.IPAddress, …)   // network → IPv4 only
```
`EndpointSettings` also carries `GlobalIPv6Address`. Capture both per network:

```csharp
// DockYarp.Docker.Discovery
public readonly record struct NetworkAddresses(string? Ipv4, string? Ipv6);

public enum AddressFamilyPreference { Ipv4, Ipv6 }   // enum, not a bool parameter (analyzer AV1564)
```

`BuildNetworks` → `Dictionary<string, NetworkAddresses>` reading `IPAddress` + `GlobalIPv6Address`. The map type
threads through the two pure helpers:
- `BackendAddressResolver.IsHostNetwork` / `Resolve` — take `IReadOnlyDictionary<string, NetworkAddresses>`; their
  logic is unchanged (host-mode is a key check; `Resolve` uses the already-selected IP).
- `NetworkAddressSelector.Select(networks, preferredNetwork, proxyNetworks, preference)` — the network-choice rules
  are unchanged (preferred → ingress-skip → reachable-restrict → ordinal-first). Once a network is chosen, pick the
  family:

```csharp
private static string? Pick(NetworkAddresses address, AddressFamilyPreference preference) =>
    preference == AddressFamilyPreference.Ipv6
        ? First(address.Ipv6, address.Ipv4)   // prefer IPv6, fall back to IPv4
        : First(address.Ipv4, address.Ipv6);  // prefer IPv4, fall back to IPv6

private static string? First(string? preferred, string? other) =>
    preferred is { Length: > 0 } ? preferred : (other is { Length: > 0 } ? other : null);
```

A network counts as "usable" when it has **any** address (either family); the fallback makes an IPv6-only backend
routable even with the default IPv4 preference (a strict improvement — before, it had no address at all).

## Option
`DockerDiscoveryOptions.PreferIpv6` (bool, default false), bound from the `Docker` section (Program.cs already binds
it). `DockerContainerSource` maps it to `AddressFamilyPreference` once and passes it to `Select`.

## Why default behavior is preserved
Existing setups declare IPv4 addresses; with `PreferIpv6=false` (default) and `NetworkAddresses(ipv4, ipv6)`, `Pick`
returns IPv4 exactly as today. A network with no IPv4 (previously filtered out as unusable) now falls back to IPv6 —
new but strictly better, and only reachable if the proxy can route IPv6 (dual-stack, which it is).

## Tests (`DockYarp.Docker.Tests`)
`NetworkAddressSelectorTests` (map values become `NetworkAddresses`):
- default (IPv4 preference) picks IPv4; existing scenarios unchanged;
- `PreferIpv6` picks the network's IPv6;
- `PreferIpv6` with no IPv6 on the chosen network falls back to IPv4;
- an IPv6-only network is routable under the default preference (fallback);
- preferred-network / ingress-skip / reachable-restriction still hold with families present.
`BackendAddressResolverTests`: updated to the new map value type; host-mode / skip logic unchanged.

## Out of scope
A live IPv6 e2e (IPv6-enabled Docker network) is deferred — the selection is fully unit-tested and the listener is
already dual-stack.
