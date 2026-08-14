namespace DockYarp.Docker.Discovery;

/// <summary>Which IP address family to prefer when a backend network has both.</summary>
public enum AddressFamilyPreference
{
    /// <summary>Prefer the IPv4 address (the default), falling back to IPv6 when absent.</summary>
    Ipv4,

    /// <summary>Prefer the IPv6 address (nginx-proxy <c>PREFER_IPV6_NETWORK</c>), falling back to IPv4 when absent.</summary>
    Ipv6,
}
