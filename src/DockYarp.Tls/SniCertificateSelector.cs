namespace DockYarp.Tls;

using System.Security.Cryptography.X509Certificates;

/// <summary>Selects the certificate for a TLS handshake by SNI host, falling back to the default.</summary>
/// <param name="store">The certificate store.</param>
/// <param name="fallback">The fallback certificate provider.</param>
public sealed class SniCertificateSelector(ICertificateStore store, DefaultCertificateProvider fallback)
{
    /// <summary>Selects the certificate for the requested host.</summary>
    /// <param name="host">The SNI host, or <see langword="null"/>.</param>
    /// <returns>The host certificate when present; otherwise the fallback.</returns>
    public X509Certificate2 Select(string? host)
    {
        if (host is { Length: > 0 } name)
        {
            X509Certificate2? found = store.Find(name);
            if (found is not null)
            {
                return found;
            }
        }

        return fallback.Certificate;
    }
}
