namespace DockYarp.Tls;

using System;

using DockYarp.Core.Models;
using DockYarp.Core.Routing;

/// <summary>Resolves the per-host TLS preset (<c>SSL_POLICY</c>) declared for a host, if any.</summary>
/// <remarks>Pure and side-effect free, mirroring <see cref="CertificateNameResolver"/>.</remarks>
public static class HostSslPolicyResolver
{
    /// <summary>Finds the <c>SSL_POLICY</c> preset declared for a host by the routing snapshot.</summary>
    /// <param name="snapshot">The routing snapshot.</param>
    /// <param name="host">The requested host.</param>
    /// <returns>The declared preset name, or <see langword="null"/> when the host declares none.</returns>
    public static string? Resolve(RouteConfigSnapshot snapshot, string host)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(host);

        foreach (RouteRule route in snapshot.Routes)
        {
            if (route.Tls is { SslPolicy: { Length: > 0 } policy }
                && HostPattern.Parse(route.HostPattern).Matches(host))
            {
                return policy;
            }
        }

        return null;
    }
}
