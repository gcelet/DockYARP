namespace DockYarp.App.Observability;

using DockYarp.AdminApi;
using DockYarp.Tls;

/// <summary>Exposes the TLS certificate store's private-key material to the opt-in dashboard download feature.</summary>
/// <param name="store">The certificate store.</param>
public sealed class CertificateExporterAdapter(ICertificateStore store) : ICertificateExporter
{
    /// <inheritdoc />
    /// <remarks>
    /// Always exports the private key plain, regardless of <see cref="TlsOptions.PrivateKeyEncryptionPassphrase"/>:
    /// at-rest encryption doesn't defend against this download path anyway (see
    /// <c>add-tls-private-key-encryption</c>'s design), and encrypting the downloaded copy would only add
    /// friction — the operator would need the passphrase separately to use it, with no corresponding security
    /// gain for a file they were already able to download.
    /// </remarks>
    public CertificateExport? Export(string host)
    {
        LoadedCertificate? certificate = store.Find(host);
        return certificate is null ? null : new CertificateExport(certificate.ExportChainPem(), certificate.ExportPrivateKeyPem(null));
    }
}
