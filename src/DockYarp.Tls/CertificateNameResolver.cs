namespace DockYarp.Tls;

using System;

using DockYarp.Core.Models;
using DockYarp.Core.Routing;

/// <summary>Resolves the shared certificate name (<c>CERT_NAME</c>) pinned for a host, if any.</summary>
/// <remarks>Pure and side-effect free so both SNI selection and certificate-availability gating share one rule.</remarks>
public static class CertificateNameResolver
{
    /// <summary>Finds the <c>CERT_NAME</c> pinned for a host by the routing snapshot.</summary>
    /// <param name="snapshot">The routing snapshot.</param>
    /// <param name="host">The requested host.</param>
    /// <returns>The pinned certificate name, or <see langword="null"/> when the host pins none.</returns>
    public static string? Resolve(RouteConfigSnapshot snapshot, string host)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(host);

        foreach (RouteRule route in snapshot.Routes)
        {
            if (route.Tls is { CertificateName: { Length: > 0 } name }
                && HostPattern.Parse(route.HostPattern).Matches(host))
            {
                return name;
            }
        }

        return null;
    }
}
