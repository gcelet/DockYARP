namespace DockYarp.Docker.Discovery;

/// <summary>A container's addresses on a single Docker network, one per family (either may be absent).</summary>
/// <param name="Ipv4">The IPv4 address, or <see langword="null"/> when the network has none.</param>
/// <param name="Ipv6">The IPv6 address, or <see langword="null"/> when the network has none.</param>
public readonly record struct NetworkAddresses(string? Ipv4, string? Ipv6);
