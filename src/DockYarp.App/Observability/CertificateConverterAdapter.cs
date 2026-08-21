namespace DockYarp.App.Observability;

using DockYarp.AdminApi;
using DockYarp.Tls;

/// <summary>Exposes the TLS certificate store's PFX-to-PEM conversion to the opt-in dashboard action.</summary>
/// <param name="store">The certificate store.</param>
/// <param name="tlsOptions">TLS options, used to expose whether private-key encryption is configured.</param>
public sealed class CertificateConverterAdapter(ICertificateStore store, TlsOptions tlsOptions) : ICertificateConverter
{
    /// <inheritdoc />
    public bool IsPfxBacked(string host) => store.IsPfxBacked(host);

    /// <inheritdoc />
    public bool PrivateKeyEncryptionConfigured => tlsOptions.PrivateKeyEncryptionPassphrase is { Length: > 0 };

    /// <inheritdoc />
    public bool RequiresKeyReencryption(string host) => store.RequiresKeyReencryption(host);

    /// <inheritdoc />
    public bool ConvertToPem(string host) => store.ConvertToPem(host);

    /// <inheritdoc />
    public bool ReencryptPrivateKey(string host) => store.ReencryptPrivateKey(host);
}
