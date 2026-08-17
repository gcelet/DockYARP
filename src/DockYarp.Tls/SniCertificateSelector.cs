namespace DockYarp.Tls;

using System;
using System.Collections.Concurrent;

using DockYarp.Core.Interfaces;

using Microsoft.Extensions.Logging;

/// <summary>Selects the certificate for a TLS handshake by SNI host, falling back to the default.</summary>
/// <param name="store">The certificate store.</param>
/// <param name="routes">The routing store, used to resolve a host's <c>CERT_NAME</c> shared certificate.</param>
/// <param name="fallback">The fallback certificate provider.</param>
/// <param name="logger">Logger for shared-certificate diagnostics.</param>
public sealed class SniCertificateSelector(
    ICertificateStore store,
    IRouteConfigStore routes,
    DefaultCertificateProvider fallback,
    ILogger<SniCertificateSelector> logger)
{
    // A pinned CERT_NAME whose certificate is absent is warned once, since SNI selection runs per handshake.
    private readonly ConcurrentDictionary<string, byte> warnedMissing = new(StringComparer.Ordinal);

    /// <summary>Selects the certificate for the requested host.</summary>
    /// <param name="host">The SNI host, or <see langword="null"/>.</param>
    /// <returns>
    /// The <c>CERT_NAME</c> shared certificate when pinned and present; otherwise the exact host certificate;
    /// otherwise the parent-domain (wildcard) certificate; otherwise the fallback.
    /// </returns>
    public LoadedCertificate Select(string? host)
    {
        if (host is { Length: > 0 } name)
        {
            if (CertificateNameResolver.Resolve(routes.Current, name) is { } certName)
            {
                if (store.Find(certName) is { } shared)
                {
                    return shared;
                }

                if (warnedMissing.TryAdd(certName, 0))
                {
                    TlsLog.SharedCertificateMissing(logger, certName);
                }
            }

            LoadedCertificate? exact = store.Find(name);
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
