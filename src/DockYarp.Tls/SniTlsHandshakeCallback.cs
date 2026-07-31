namespace DockYarp.Tls;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;

/// <summary>Assembles the TLS session for each HTTPS connection, keyed by the SNI host.</summary>
/// <remarks>
/// Kestrel's per-connection handshake callback bypasses <c>ConfigureHttpsDefaults</c> and the default
/// certificate, so this type reassembles the certificate, protocol floor, cipher policy, and mutual-TLS policy
/// itself. The global TLS posture is resolved once and captured; only the certificate lookup runs per handshake.
/// </remarks>
public sealed class SniTlsHandshakeCallback
{
    private readonly SniCertificateSelector selector;
    private readonly SslProtocols enabledProtocols;
    private readonly List<SslApplicationProtocol> applicationProtocols;
    private readonly CipherSuitesPolicy? cipherSuitesPolicy;
    private readonly bool mutualTls;
    private readonly RemoteCertificateValidationCallback? validateClientCertificate;
    private readonly TlsHandshakeCallbackOptions callbackOptions;

    /// <summary>Captures the global TLS posture and mutual-TLS wiring.</summary>
    /// <param name="selector">The SNI certificate selector (also resolves the fallback certificate).</param>
    /// <param name="clientCertificates">Validator for client certificates (mutual TLS).</param>
    /// <param name="options">TLS options carrying the posture (minimum version, ciphers, protocols, SSL policy).</param>
    public SniTlsHandshakeCallback(
        SniCertificateSelector selector,
        ClientCertificateValidator clientCertificates,
        TlsOptions options)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(clientCertificates);
        ArgumentNullException.ThrowIfNull(options);

        this.selector = selector;

        SslPolicyResolution effective =
            SslPolicyPresets.Resolve(options.SslPolicy, options.MinimumTlsVersion, options.CipherSuites);
        enabledProtocols = TlsHardening.ToSslProtocols(effective.MinimumTlsVersion);
        applicationProtocols =
            TlsHardening.ToApplicationProtocols(TlsHardening.ParseHttpProtocols(options.HttpProtocols));

        ImmutableArray<TlsCipherSuite> ciphers = TlsHardening.ParseCipherSuites(effective.CipherSuites);

        // CipherSuitesPolicy is only supported on Linux/macOS; elsewhere the OS negotiates its own defaults.
        cipherSuitesPolicy = !ciphers.IsEmpty && (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            ? new CipherSuitesPolicy(ciphers)
            : null;

        mutualTls = clientCertificates.HasClientCa;

        // One delegate instance reused across every handshake (captures the validator; no per-connection closure).
        // AllowCertificate semantics: a client presenting no certificate is accepted at the TLS layer; a
        // presented certificate must chain to the configured CA.
        validateClientCertificate = mutualTls
            ? (_, certificate, _, _) => certificate switch
            {
                null => true,
                X509Certificate2 clientCertificate => clientCertificates.Validate(clientCertificate),
                _ => false,
            }
            : null;

        callbackOptions = new TlsHandshakeCallbackOptions { OnConnection = OnConnectionAsync };
    }

    /// <summary>Gets the Kestrel handshake callback options wired to <see cref="BuildOptions"/>.</summary>
    public TlsHandshakeCallbackOptions Options => callbackOptions;

    /// <summary>Builds the server authentication options for a handshake targeting <paramref name="host"/>.</summary>
    /// <param name="host">The SNI host (empty or <see langword="null"/> when the client sent no SNI extension).</param>
    /// <returns>The assembled <see cref="SslServerAuthenticationOptions"/>.</returns>
    public SslServerAuthenticationOptions BuildOptions(string? host)
    {
        SslServerAuthenticationOptions authentication = new()
        {
            ServerCertificate = selector.Select(host),
            EnabledSslProtocols = enabledProtocols,
            ApplicationProtocols = applicationProtocols,
        };

        if (cipherSuitesPolicy is not null)
        {
            authentication.CipherSuitesPolicy = cipherSuitesPolicy;
        }

        if (mutualTls)
        {
            authentication.ClientCertificateRequired = true;
            authentication.RemoteCertificateValidationCallback = validateClientCertificate;
        }

        return authentication;
    }

    private ValueTask<SslServerAuthenticationOptions> OnConnectionAsync(TlsHandshakeCallbackContext context) =>
        ValueTask.FromResult(BuildOptions(context.ClientHelloInfo.ServerName));
}
