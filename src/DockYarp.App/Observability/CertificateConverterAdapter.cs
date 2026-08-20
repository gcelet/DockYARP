namespace DockYarp.App.Observability;

using DockYarp.AdminApi;
using DockYarp.Tls;

/// <summary>Exposes the TLS certificate store's PFX-to-PEM conversion to the opt-in dashboard action.</summary>
/// <param name="store">The certificate store.</param>
public sealed class CertificateConverterAdapter(ICertificateStore store) : ICertificateConverter
{
    /// <inheritdoc />
    public bool IsPfxBacked(string host) => store.IsPfxBacked(host);

    /// <inheritdoc />
    public bool ConvertToPem(string host) => store.ConvertToPem(host);
}
