namespace DockYarp.Tls;

using System;

using DockYarp.Core.Models;
using DockYarp.Core.Routing;

/// <summary>Resolves the per-host client-certificate requirement declared for a host, if any.</summary>
/// <remarks>Pure and side-effect free, mirroring <see cref="HostSslPolicyResolver"/>.</remarks>
public static class HostClientCertificateResolver
{
    /// <summary>Finds the client-certificate requirement declared for a host by the routing snapshot.</summary>
    /// <param name="snapshot">The routing snapshot.</param>
    /// <param name="host">The requested host.</param>
    /// <returns>The declared requirement, or <see cref="ClientCertificateRequirement.None"/> when no route
    /// matches the host.</returns>
    public static ClientCertificateRequirement Resolve(RouteConfigSnapshot snapshot, string host)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(host);

        foreach (RouteRule route in snapshot.Routes)
        {
            if (route.ClientCertificate != ClientCertificateRequirement.None
                && HostPattern.Parse(route.HostPattern).Matches(host))
            {
                return route.ClientCertificate;
            }
        }

        return ClientCertificateRequirement.None;
    }
}
