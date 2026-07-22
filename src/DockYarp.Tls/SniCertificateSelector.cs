namespace DockYarp.Tls;

using System;
using System.Security.Cryptography.X509Certificates;

/// <summary>Selects the certificate for a TLS handshake by SNI host, falling back to the default.</summary>
/// <param name="store">The certificate store.</param>
/// <param name="fallback">The fallback certificate provider.</param>
public sealed class SniCertificateSelector(ICertificateStore store, DefaultCertificateProvider fallback)
{
    /// <summary>Selects the certificate for the requested host.</summary>
    /// <param name="host">The SNI host, or <see langword="null"/>.</param>
    /// <returns>
    /// The exact host certificate when present; otherwise the parent-domain (wildcard) certificate; otherwise the fallback.
    /// </returns>
    public X509Certificate2 Select(string? host)
    {
        if (host is { Length: > 0 } name)
        {
            X509Certificate2? exact = store.Find(name);
            if (exact is not null)
            {
                return exact;
            }

            // A wildcard certificate (*.example.com) is provided under its base domain (example.com).
            if (ParentDomain(name) is { } parent && store.Find(parent) is { } wildcard)
            {
                return wildcard;
            }
        }

        return fallback.Certificate;
    }

    /// <summary>Returns the parent domain (leftmost label removed) when it is still a multi-label domain.</summary>
    private static string? ParentDomain(string host)
    {
        int dot = host.IndexOf('.', StringComparison.Ordinal);
        if (dot < 0)
        {
            return null;
        }

        string parent = host[(dot + 1)..];
        return parent.Contains('.', StringComparison.Ordinal) ? parent : null;
    }
}
