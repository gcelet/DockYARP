namespace DockYarp.Tls;

using System;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Options;

/// <summary>Binds the data-plane endpoints and attaches the per-connection TLS handshake callback.</summary>
/// <remarks>
/// The HTTPS endpoint assembles its TLS options per connection from the SNI host (<see cref="SniTlsHandshakeCallback"/>),
/// which bypasses <c>ConfigureHttpsDefaults</c> and the default certificate; the endpoints are therefore bound
/// explicitly. Configuring endpoints in code makes Kestrel ignore host-injected <c>ASPNETCORE_URLS</c>/<c>*_PORTS</c>
/// (a benign "Overriding address(es)…" warning), so there is no double-bind.
/// </remarks>
/// <param name="handshakeCallback">The per-connection TLS options builder.</param>
/// <param name="options">TLS options carrying the enabled HTTP protocols.</param>
/// <param name="endpoints">The data-plane HTTP/HTTPS ports.</param>
public sealed class KestrelTlsConfigurator(
    SniTlsHandshakeCallback handshakeCallback,
    TlsOptions options,
    ServerEndpointOptions endpoints)
    : IConfigureOptions<KestrelServerOptions>
{
    /// <inheritdoc />
    public void Configure(KestrelServerOptions serverOptions)
    {
        ArgumentNullException.ThrowIfNull(serverOptions);

        HttpProtocols httpsProtocols = TlsHardening.ParseHttpProtocols(options.HttpProtocols);
        TlsHandshakeCallbackOptions callbackOptions = handshakeCallback.Options;

        // Plaintext HTTP endpoint: ACME HTTP-01 challenge + HTTP→HTTPS redirects. HTTP/2 requires TLS, so this
        // endpoint negotiates HTTP/1.1 only.
        serverOptions.ListenAnyIP(endpoints.HttpPort, listen => listen.Protocols = HttpProtocols.Http1);

        // HTTPS endpoint: the TLS session (certificate, protocols, ciphers, mTLS) is assembled per connection
        // from the SNI host.
        serverOptions.ListenAnyIP(endpoints.HttpsPort, listen =>
        {
            listen.Protocols = httpsProtocols;
            listen.UseHttps(callbackOptions);
        });
    }
}
