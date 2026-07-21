namespace DockYarp.Tls;

using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

/// <summary>Wires the SNI certificate selector into Kestrel's HTTPS defaults.</summary>
/// <param name="selector">The SNI certificate selector.</param>
public sealed class KestrelTlsConfigurator(SniCertificateSelector selector) : IConfigureOptions<KestrelServerOptions>
{
    /// <inheritdoc />
    public void Configure(KestrelServerOptions options)
    {
        options.ConfigureHttpsDefaults(https => https.ServerCertificateSelector = (_, host) => selector.Select(host));
    }
}
