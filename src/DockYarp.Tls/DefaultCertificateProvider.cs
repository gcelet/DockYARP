namespace DockYarp.Tls;

using System;
using System.Security.Cryptography.X509Certificates;

/// <summary>Holds the self-signed fallback certificate, created once at startup.</summary>
public sealed class DefaultCertificateProvider : IDisposable
{
    /// <summary>Initializes the provider, generating the fallback certificate.</summary>
    public DefaultCertificateProvider()
    {
        Certificate = DefaultCertificateFactory.CreateSelfSigned("dockyarp.local");
    }

    /// <summary>Gets the fallback certificate.</summary>
    public X509Certificate2 Certificate { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        Certificate.Dispose();
        GC.SuppressFinalize(this);
    }
}
