namespace DockYarp.Tls;

using System;
using System.IO.Abstractions;
using System.Security.Cryptography.X509Certificates;

/// <summary>Validates client certificates against a configured CA (for mutual TLS).</summary>
/// <remarks>The CA is loaded once from a PEM bundle; validation is offline (custom trust, no revocation).</remarks>
public sealed class ClientCertificateValidator
{
    private readonly X509Certificate2Collection caCertificates = [];

    /// <summary>Loads the client CA certificate(s) from the configured path, if any.</summary>
    /// <param name="options">TLS options carrying the client CA path.</param>
    /// <param name="fileSystem">Filesystem abstraction used to read the CA file.</param>
    public ClientCertificateValidator(TlsOptions options, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fileSystem);

        if (options.ClientCaCertificatePath is { Length: > 0 } path && fileSystem.File.Exists(path))
        {
            caCertificates.ImportFromPem(fileSystem.File.ReadAllText(path));
        }
    }

    /// <summary>Gets a value indicating whether a client CA is configured (mutual TLS is enabled).</summary>
    public bool HasClientCa => caCertificates.Count > 0;

    /// <summary>Validates that a client certificate chains to the configured CA.</summary>
    /// <param name="certificate">The presented client certificate.</param>
    /// <returns><see langword="true"/> when the certificate chains to the CA; otherwise <see langword="false"/>.</returns>
    public bool Validate(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        if (!HasClientCa)
        {
            return false;
        }

        using X509Chain chain = new();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.CustomTrustStore.AddRange(caCertificates);
        return chain.Build(certificate);
    }
}
