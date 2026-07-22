namespace DockYarp.App.Security;

using System;

using DockYarp.Security;
using DockYarp.Tls;

/// <summary>Adapts the certificate store to <see cref="ICertificateAvailability"/> for the security layer.</summary>
/// <param name="store">The certificate store.</param>
internal sealed class CertificateAvailabilityAdapter(ICertificateStore store) : ICertificateAvailability
{
    /// <inheritdoc />
    public bool IsAvailable(string host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (store.Find(host) is not null)
        {
            return true;
        }

        // A wildcard certificate (*.example.com) is stored under its base domain (example.com).
        int dot = host.IndexOf('.', StringComparison.Ordinal);
        if (dot < 0)
        {
            return false;
        }

        string parent = host[(dot + 1)..];
        return parent.Contains('.', StringComparison.Ordinal) && store.Find(parent) is not null;
    }
}
