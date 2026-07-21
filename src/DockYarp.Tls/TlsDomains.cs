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
        return
        [
            .. snapshot.Routes
                .Where(route => route.Tls is { } tls && !string.IsNullOrEmpty(tls.CertificateHost))
                .Select(route => new DesiredCertificate(route.Tls!.CertificateHost, route.Tls!.ContactEmail))
                .DistinctBy(desired => desired.Host, StringComparer.OrdinalIgnoreCase),
        ];
    }
}
