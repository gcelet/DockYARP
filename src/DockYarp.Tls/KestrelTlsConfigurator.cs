namespace DockYarp.Tls;

using System;
using System.Collections.Immutable;
using System.Globalization;
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
        SslPolicyResolution effective =
            SslPolicyPresets.Resolve(options.SslPolicy, options.MinimumTlsVersion, options.CipherSuites);
        ImmutableArray<TlsCipherSuite> ciphers = TlsHardening.ParseCipherSuites(effective.CipherSuites);

        serverOptions.ConfigureHttpsDefaults(https =>
        {
            https.SslProtocols = TlsHardening.ToSslProtocols(effective.MinimumTlsVersion);
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

        int? httpPort = ResolveHttpPort();
        serverOptions.ConfigureEndpointDefaults(listen =>
        {
            // HTTP/2 requires TLS: the plaintext HTTP endpoint (ACME challenges + redirects) negotiates HTTP/1.1
            // only, while the HTTPS endpoint keeps the configured protocols. Matching on the known HTTP port
            // never downgrades the TLS endpoint; if the port is unknown, protocols are left as configured.
            listen.Protocols = httpPort is { } port && listen.IPEndPoint?.Port == port
                ? HttpProtocols.Http1
                : TlsHardening.ParseHttpProtocols(options.HttpProtocols);
        });
    }

    private static int? ResolveHttpPort()
    {
        string? value = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS");
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string[] ports = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return ports.Length > 0 && int.TryParse(ports[0], CultureInfo.InvariantCulture, out int port) ? port : null;
    }
}
