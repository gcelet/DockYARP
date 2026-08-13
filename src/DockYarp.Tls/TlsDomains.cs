namespace DockYarp.Tls;

using System;
using System.Collections.Generic;
using System.Linq;

using DockYarp.Core.Models;

/// <summary>Derives the set of hosts that need a certificate from the routing snapshot.</summary>
public static class TlsDomains
{
    /// <summary>Returns the distinct hosts declaring TLS metadata, with their contact email.</summary>
    /// <param name="snapshot">The routing snapshot.</param>
    /// <returns>The desired certificate hosts.</returns>
    public static IReadOnlyList<DesiredCertificate> Desired(RouteConfigSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // A CERT_NAME host uses an operator-supplied shared certificate, so it is not ACME-provisioned.
        return
        [
            .. snapshot.Routes
                .Where(route => route.Tls is { CertificateHost.Length: > 0, CertificateName: null or "" } tls
                    && tls.Method != HttpsMethod.NoHttps)
                .Select(route => new DesiredCertificate(route.Tls!.CertificateHost, route.Tls!.ContactEmail))
                .DistinctBy(desired => desired.Host, StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>Returns the route-derived desired hosts plus any reserved host not already covered by a route.</summary>
    /// <param name="snapshot">The routing snapshot.</param>
    /// <param name="reserved">Hosts to provision that are not derived from routes (for example the admin host).</param>
    /// <returns>The merged desired certificate hosts, deduplicated by host (a route wins over a reserved clash).</returns>
    public static IReadOnlyList<DesiredCertificate> Desired(
        RouteConfigSnapshot snapshot,
        IReadOnlyList<DesiredCertificate> reserved)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(reserved);

        IReadOnlyList<DesiredCertificate> fromRoutes = Desired(snapshot);
        if (reserved.Count == 0)
        {
            return fromRoutes;
        }

        HashSet<string> seen = new(fromRoutes.Select(desired => desired.Host), StringComparer.OrdinalIgnoreCase);

        // Route desires win on a name clash (a real route already provisions that host); append reserved hosts
        // not already covered, deduping reserved-vs-reserved by host as well.
        return
        [
            .. fromRoutes,
            .. reserved.Where(desired => seen.Add(desired.Host)),
        ];
    }
}
