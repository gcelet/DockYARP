## 1. Model + option (AG-DD)
- [x] 1.1 `NetworkAddresses(string? Ipv4, string? Ipv6)` readonly record struct + `AddressFamilyPreference { Ipv4, Ipv6 }` enum
- [x] 1.2 `DockerDiscoveryOptions.PreferIpv6` (bool, default false) with XML doc

## 2. Discovery (AG-DD)
- [x] 2.1 `DockerContainerSource.BuildNetworks`: read both `IPAddress` and `GlobalIPv6Address` into `NetworkAddresses`
- [x] 2.2 `ResolveAddress`: map `PreferIpv6` → `AddressFamilyPreference`, pass it to `NetworkAddressSelector.Select`

## 3. Selection (AG-DD)
- [x] 3.1 `NetworkAddressSelector.Select`: take `IReadOnlyDictionary<string, NetworkAddresses>` + preference; keep the
  network-choice rules; pick the family within the chosen network (prefer selected family, fall back to the other)
- [x] 3.2 `BackendAddressResolver.IsHostNetwork` / `Resolve`: take the new map value type (logic unchanged)

## 4. Tests (AG-DD)
- [x] 4.1 `NetworkAddressSelectorTests`: default picks IPv4; `PreferIpv6` picks IPv6; IPv6-preference with no IPv6
  falls back to IPv4; IPv6-only network routable under default; preferred/ingress/reachable rules still hold
- [x] 4.2 `BackendAddressResolverTests`: updated to the new map value type; host-mode/skip unchanged

## 5. Docs (AG-DOC — new app-config key)
- [x] 5.1 docs site `configuration.md`: document `Docker:PreferIpv6`
- [x] 5.2 `features.md`: note IPv6 listening is dual-stack by default (no toggle) and backends can be reached over IPv6

## 6. Verify (AG-DD)
- [x] 6.1 Nuke `Test` gate green (unit), warnings-as-errors clean
