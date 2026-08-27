namespace DockYarp.App.Observability;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using DockYarp.AdminApi;
using DockYarp.Core.Interfaces;
using DockYarp.Core.Models;
using DockYarp.Tls;

/// <summary>Exposes ACME certificate revocation to the opt-in dashboard action.</summary>
/// <param name="store">The certificate store.</param>
/// <param name="acme">The ACME client.</param>
/// <param name="routes">The routing store — resolves a host's contact email so the revocation request is
/// signed with the same persisted account key that issued it.</param>
/// <param name="reserved">Non-route hosts (for example the admin host), matching
/// <see cref="CertificateProvisioningService"/>'s own desired-hosts resolution.</param>
public sealed class CertificateRevokerAdapter(
    ICertificateStore store, IAcmeClient acme, IRouteConfigStore routes, IReservedCertificateHosts reserved)
    : ICertificateRevoker
{
    /// <inheritdoc />
    public async Task<bool> RevokeCertificateAsync(string host, CancellationToken cancellationToken)
    {
        LoadedCertificate? certificate = store.Find(host);
        if (certificate is null)
        {
            return false;
        }

        string? email = ResolveContactEmail(host);
        await acme.RevokeCertificateAsync(host, email, certificate.Leaf, cancellationToken).ConfigureAwait(false);
        store.Remove(host);
        return true;
    }

    // Mirrors CertificateProvisioningService's own desired-hosts resolution (route + reserved, wildcard
    // stripped to its parent domain) so the resolved email always matches what the certificate was actually
    // issued with — a mismatch would resolve a different (email, endpoint) account key than the one used to
    // request it.
    private string? ResolveContactEmail(string storageKey)
    {
        RouteConfigSnapshot snapshot = routes.Current;
        DesiredCertificate? match = TlsDomains.Desired(snapshot, reserved.Reserved)
            .FirstOrDefault(desired => MatchesStorageKey(desired.Host, storageKey));
        return match?.Email;
    }

    private static bool MatchesStorageKey(string desiredHost, string storageKey)
    {
        string parentDomain = desiredHost.StartsWith("*.", StringComparison.Ordinal) ? desiredHost[2..] : desiredHost;
        return string.Equals(parentDomain, storageKey, StringComparison.OrdinalIgnoreCase);
    }
}
