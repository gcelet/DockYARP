namespace DockYarp.Tls;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using DockYarp.Core.Interfaces;

using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Logging;

/// <summary>Assembles the TLS session for each HTTPS connection, keyed by the SNI host.</summary>
/// <remarks>
/// Kestrel's per-connection handshake callback bypasses <c>ConfigureHttpsDefaults</c> and the default
/// certificate, so this type reassembles the certificate, protocol floor, cipher policy, and mutual-TLS policy
/// itself. The global posture and each preset are prepared once; per handshake, a host that declares an
/// <c>SSL_POLICY</c> preset overrides the global protocol floor and cipher policy, while only the certificate
/// and per-host policy lookups run.
/// </remarks>
public sealed class SniTlsHandshakeCallback
{
    private readonly SniCertificateSelector selector;
    private readonly IRouteConfigStore routes;
    private readonly ILogger<SniTlsHandshakeCallback> logger;
    private readonly List<SslApplicationProtocol> applicationProtocols;
    private readonly PreparedPolicy globalPolicy;
    private readonly IReadOnlyDictionary<string, PreparedPolicy> presetPolicies;
    private readonly bool mutualTls;
    private readonly RemoteCertificateValidationCallback? validateClientCertificate;
    private readonly TlsHandshakeCallbackOptions callbackOptions;
    private readonly ConcurrentDictionary<string, byte> warnedUnknownPolicy = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Captures the global TLS posture, the per-preset postures, and the mutual-TLS wiring.</summary>
    /// <param name="selector">The SNI certificate selector (also resolves the fallback certificate).</param>
    /// <param name="routes">The routing store, used to resolve a host's <c>SSL_POLICY</c> preset.</param>
    /// <param name="clientCertificates">Validator for client certificates (mutual TLS).</param>
    /// <param name="options">TLS options carrying the global posture (minimum version, ciphers, protocols, SSL policy).</param>
    /// <param name="logger">Logger for the per-host policy diagnostic.</param>
    public SniTlsHandshakeCallback(
        SniCertificateSelector selector,
        IRouteConfigStore routes,
        ClientCertificateValidator clientCertificates,
        TlsOptions options,
        ILogger<SniTlsHandshakeCallback> logger)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(clientCertificates);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.selector = selector;
        this.routes = routes;
        this.logger = logger;

        applicationProtocols =
            TlsHardening.ToApplicationProtocols(TlsHardening.ParseHttpProtocols(options.HttpProtocols));

        // The global posture; a per-host SSL_POLICY preset overrides it at handshake time.
        globalPolicy = Prepare(SslPolicyPresets.Resolve(options.SslPolicy, options.MinimumTlsVersion, options.CipherSuites));

        // Each preset is prepared once so no cipher parsing runs per handshake. A per-host preset fully replaces
        // the posture (the global explicit ciphers do not bleed into it), matching nginx-proxy's per-vhost policy.
        Dictionary<string, PreparedPolicy> presets = new(StringComparer.OrdinalIgnoreCase);
        foreach (string name in SslPolicyPresets.KnownPresetNames)
        {
            presets[name] = Prepare(SslPolicyPresets.Resolve(name, TlsVersion.Tls12, configuredCiphers: null));
        }

        presetPolicies = presets;

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
        PreparedPolicy policy = ResolvePolicy(host);
        SslServerAuthenticationOptions authentication = new()
        {
            ServerCertificate = selector.Select(host),
            EnabledSslProtocols = policy.Protocols,
            ApplicationProtocols = applicationProtocols,
        };

        if (policy.Ciphers is not null)
        {
            authentication.CipherSuitesPolicy = policy.Ciphers;
        }

        if (mutualTls)
        {
            authentication.ClientCertificateRequired = true;
            authentication.RemoteCertificateValidationCallback = validateClientCertificate;
        }

        return authentication;
    }

    private static PreparedPolicy Prepare(SslPolicyResolution resolution)
    {
        ImmutableArray<TlsCipherSuite> ciphers = TlsHardening.ParseCipherSuites(resolution.CipherSuites);

        // CipherSuitesPolicy is only supported on Linux/macOS; elsewhere the OS negotiates its own defaults.
        CipherSuitesPolicy? cipherPolicy = !ciphers.IsEmpty && (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            ? new CipherSuitesPolicy(ciphers)
            : null;
        return new PreparedPolicy(TlsHardening.ToSslProtocols(resolution.MinimumTlsVersion), cipherPolicy);
    }

    private PreparedPolicy ResolvePolicy(string? host)
    {
        if (host is not { Length: > 0 } name)
        {
            return globalPolicy;
        }

        if (HostSslPolicyResolver.Resolve(routes.Current, name) is not { Length: > 0 } policyName)
        {
            return globalPolicy;
        }

        if (presetPolicies.TryGetValue(policyName, out PreparedPolicy prepared))
        {
            return prepared;
        }

        // Unknown per-host preset: keep the global posture, warned once per distinct value.
        if (warnedUnknownPolicy.TryAdd(policyName, 0))
        {
            TlsLog.UnsupportedSslPolicy(logger, policyName);
        }

        return globalPolicy;
    }

    private ValueTask<SslServerAuthenticationOptions> OnConnectionAsync(TlsHandshakeCallbackContext context) =>
        ValueTask.FromResult(BuildOptions(context.ClientHelloInfo.ServerName));

    private readonly record struct PreparedPolicy(SslProtocols Protocols, CipherSuitesPolicy? Ciphers);
}
