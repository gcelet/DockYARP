namespace DockYarp.Tls;

using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

/// <summary>Wires the SNI certificate selector (and a default certificate) into Kestrel's HTTPS defaults.</summary>
/// <remarks>
/// A ports-only HTTPS endpoint (<c>ASPNETCORE_HTTPS_PORTS</c>) requires a default certificate to start, so
/// the self-signed fallback is set as the default; the selector overrides it per SNI host at runtime.
/// </remarks>
/// <param name="selector">The SNI certificate selector.</param>
/// <param name="fallback">Provider of the self-signed fallback certificate (used as the default).</param>
public sealed class KestrelTlsConfigurator(SniCertificateSelector selector, DefaultCertificateProvider fallback)
    : IConfigureOptions<KestrelServerOptions>
{
    /// <inheritdoc />
    public void Configure(KestrelServerOptions options)
    {
        options.ConfigureHttpsDefaults(https =>
        {
            https.ServerCertificate = fallback.Certificate;
            https.ServerCertificateSelector = (_, host) => selector.Select(host);
        });
    }
}
