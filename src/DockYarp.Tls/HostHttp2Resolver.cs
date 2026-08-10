namespace DockYarp.Tls;

using System;

using DockYarp.Core.Models;
using DockYarp.Core.Routing;

/// <summary>Resolves the per-host frontend HTTP/2 toggle (<c>DOCKYARP_HTTP2</c>) declared for a host, if any.</summary>
/// <remarks>Pure and side-effect free, mirroring <see cref="HostSslPolicyResolver"/>.</remarks>
public static class HostHttp2Resolver
{
    /// <summary>Finds the per-host HTTP/2 toggle declared for a host by the routing snapshot.</summary>
    /// <param name="snapshot">The routing snapshot.</param>
    /// <param name="host">The requested host.</param>
    /// <returns>The declared toggle (<see langword="true"/>/<see langword="false"/>), or <see langword="null"/> when the host declares none.</returns>
    public static bool? Resolve(RouteConfigSnapshot snapshot, string host)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(host);

        foreach (RouteRule route in snapshot.Routes)
        {
            if (route.Tls is { Http2Enabled: { } enabled }
                && HostPattern.Parse(route.HostPattern).Matches(host))
            {
                return enabled;
            }
        }

        return null;
    }
}
