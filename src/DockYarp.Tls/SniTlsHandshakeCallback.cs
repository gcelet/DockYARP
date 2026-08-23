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
using DockYarp.Core.Models;

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

    // The ALPN list a host advertises when it disables HTTP/2; a strict subset of the global set, so it never
    // offers a protocol the listener (bound from the global protocols) cannot process.
    private readonly List<SslApplicationProtocol> http1OnlyProtocols = [SslApplicationProtocol.Http11];
    private readonly PreparedPolicy globalPolicy;
    private readonly IReadOnlyDictionary<string, PreparedPolicy> presetPolicies;
    private readonly bool mutualTls;
    private readonly RemoteCertificateValidationCallback? strictValidateClientCertificate;
    private readonly RemoteCertificateValidationCallback? permissiveValidateClientCertificate;
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

        // Two delegate instances reused across every handshake (capture the validator; no per-connection
        // closure). Strict (Required hosts): a client presenting no certificate is accepted at the TLS layer; a
        // presented certificate must chain to the configured CA and not be revoked, or the handshake fails.
        // Permissive (Optional hosts): the handshake never fails on the certificate's trust outcome — an
        // untrusted/revoked/absent certificate all proceed, deferring the verification decision to
        // ClientCertificateMiddleware (see design.md's Decisions: computed once there, not re-validated here).
        strictValidateClientCertificate = mutualTls
            ? (_, certificate, _, _) => certificate switch
            {
                null => true,
                X509Certificate2 clientCertificate => clientCertificates.Validate(clientCertificate),
                _ => false,
            }
            : null;

        permissiveValidateClientCertificate = mutualTls ? (_, _, _, _) => true : null;

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
        LoadedCertificate selected = selector.Select(host);
        SslServerAuthenticationOptions authentication = new()
        {
            // A bare ServerCertificate makes SslStream build its own chain via system-store-dependent logic,
            // ignoring any intermediates bagged alongside the leaf (see the TLS/SSL best-practices doc). An
            // explicit context with `offline: true` sends exactly the chain this proxy was given, with no
            // network/AIA fetch attempt during the handshake.
            ServerCertificateContext = SslStreamCertificateContext.Create(
                selected.Leaf,
                [.. selected.Additional],
                offline: true),
            EnabledSslProtocols = policy.Protocols,
            ApplicationProtocols = ResolveApplicationProtocols(host),
        };

        if (policy.Ciphers is not null)
        {
            authentication.CipherSuitesPolicy = policy.Ciphers;
        }

        if (mutualTls)
        {
            // No SNI: which host's policy would apply is unknowable, so fall back to the pre-host-aware
            // behavior (always request + strictly validate) rather than silently disabling mTLS for the
            // connection — matches ResolvePolicy's own no-SNI-falls-back-to-global precedent.
            ClientCertificateRequirement requirement = host is { Length: > 0 } sniHost
                ? HostClientCertificateResolver.Resolve(routes.Current, sniHost)
                : ClientCertificateRequirement.Required;
            if (requirement != ClientCertificateRequirement.None)
            {
                authentication.ClientCertificateRequired = true;
                authentication.RemoteCertificateValidationCallback = requirement == ClientCertificateRequirement.Required
                    ? strictValidateClientCertificate
                    : permissiveValidateClientCertificate;
            }
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

    private List<SslApplicationProtocol> ResolveApplicationProtocols(string? host)
    {
        // A host that explicitly disables HTTP/2 advertises HTTP/1.1 only; unset (or true) keeps the global set,
        // which already reflects what the listener can process — so enabling beyond it is a no-op by construction.
        if (host is { Length: > 0 } name && HostHttp2Resolver.Resolve(routes.Current, name) is false)
        {
            return http1OnlyProtocols;
        }

        return applicationProtocols;
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
