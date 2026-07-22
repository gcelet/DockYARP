namespace DockYarp.Tls;

using System;
using System.Collections.Immutable;
using System.Net.Security;

using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Options;

/// <summary>Wires the SNI certificate selector, default certificate, and TLS hardening into Kestrel.</summary>
/// <remarks>
/// A ports-only HTTPS endpoint (<c>ASPNETCORE_HTTPS_PORTS</c>) requires a default certificate to start, so
/// the self-signed fallback is set as the default; the selector overrides it per SNI host at runtime.
/// </remarks>
/// <param name="selector">The SNI certificate selector.</param>
/// <param name="fallback">Provider of the self-signed fallback certificate (used as the default).</param>
/// <param name="options">TLS options carrying the hardening settings.</param>
/// <param name="clientCertificates">Validator used to verify client certificates against the configured CA.</param>
public sealed class KestrelTlsConfigurator(
    SniCertificateSelector selector,
    DefaultCertificateProvider fallback,
    TlsOptions options,
    ClientCertificateValidator clientCertificates)
    : IConfigureOptions<KestrelServerOptions>
{
    /// <inheritdoc />
    public void Configure(KestrelServerOptions serverOptions)
    {
        ImmutableArray<TlsCipherSuite> ciphers = TlsHardening.ParseCipherSuites(options.CipherSuites);

        serverOptions.ConfigureHttpsDefaults(https =>
        {
            https.SslProtocols = TlsHardening.ToSslProtocols(options.MinimumTlsVersion);
            https.ServerCertificate = fallback.Certificate;
            https.ServerCertificateSelector = (_, host) => selector.Select(host);

            if (!ciphers.IsEmpty)
            {
                https.OnAuthenticate = (_, sslOptions) =>
                {
                    // CipherSuitesPolicy is only supported on Linux/macOS; the inline guard scopes it for the analyzer.
                    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                    {
                        sslOptions.CipherSuitesPolicy = new CipherSuitesPolicy(ciphers);
                    }
                };
            }

            // Mutual TLS: request a client certificate and accept only those chaining to the configured CA.
            if (clientCertificates.HasClientCa)
            {
                https.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
                https.ClientCertificateValidation = (certificate, _, _) => clientCertificates.Validate(certificate);
            }
        });

        serverOptions.ConfigureEndpointDefaults(listen =>
            listen.Protocols = TlsHardening.ParseHttpProtocols(options.HttpProtocols));
    }
}
