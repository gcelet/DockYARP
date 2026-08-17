namespace DockYarp.Tls;

using System;
using System.IO.Abstractions;
using System.Security.Cryptography.X509Certificates;

/// <summary>Holds the SNI fallback certificate, created once at startup.</summary>
/// <remarks>
/// Prefers an operator-supplied <c>default.crt</c>/<c>default.key</c> in the certificate directory; otherwise a
/// self-signed certificate is generated so unknown-host handshakes are still handled deterministically.
/// </remarks>
public sealed class DefaultCertificateProvider : IDisposable
{
    /// <summary>Initializes the provider, loading or generating the fallback certificate.</summary>
    /// <param name="options">TLS options (certificate directory).</param>
    /// <param name="fileSystem">The file system abstraction.</param>
    public DefaultCertificateProvider(TlsOptions options, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fileSystem);

        string certPath = fileSystem.Path.Combine(options.CertificateDirectory, "default.crt");
        string keyPath = fileSystem.Path.Combine(options.CertificateDirectory, "default.key");
        Certificate = PemCertificateLoader.TryLoad(fileSystem, certPath, keyPath, out LoadedCertificate provided)
            ? provided
            : new LoadedCertificate(DefaultCertificateFactory.CreateSelfSigned("dockyarp.local"), []);
    }

    /// <summary>Gets the fallback certificate.</summary>
    public LoadedCertificate Certificate { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        Certificate.Leaf.Dispose();
        foreach (X509Certificate2 additional in Certificate.Additional)
        {
            additional.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
