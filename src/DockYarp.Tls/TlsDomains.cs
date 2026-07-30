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
}
