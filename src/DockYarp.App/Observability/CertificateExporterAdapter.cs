namespace DockYarp.App.Observability;

using DockYarp.AdminApi;
using DockYarp.Tls;

/// <summary>Exposes the TLS certificate store's private-key material to the opt-in dashboard download feature.</summary>
/// <param name="store">The certificate store.</param>
public sealed class CertificateExporterAdapter(ICertificateStore store) : ICertificateExporter
{
    /// <inheritdoc />
    public CertificateExport? Export(string host)
    {
        LoadedCertificate? certificate = store.Find(host);
        return certificate is null ? null : new CertificateExport(certificate.ExportChainPem(), certificate.ExportPrivateKeyPem());
    }
}
