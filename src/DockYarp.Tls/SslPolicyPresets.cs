namespace DockYarp.Tls;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

/// <summary>Resolves a named <c>SSL_POLICY</c> preset into a concrete TLS version and cipher list.</summary>
/// <remarks>Pure and side-effect free so the mapping can be unit tested without starting Kestrel.</remarks>
public static class SslPolicyPresets
{
    // Mozilla server-side TLS recommendations, expressed as IANA suite names; unknown names are dropped later
    // by TlsHardening.ParseCipherSuites, so the presets stay robust across platforms.
    private static readonly ImmutableArray<string> Tls13Suites =
        ["TLS_AES_128_GCM_SHA256", "TLS_AES_256_GCM_SHA384", "TLS_CHACHA20_POLY1305_SHA256"];

    private static readonly ImmutableArray<string> IntermediateSuites =
    [
        .. Tls13Suites,
        "TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256",
        "TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256",
        "TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384",
        "TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384",
        "TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256",
        "TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256",
        "TLS_DHE_RSA_WITH_AES_128_GCM_SHA256",
        "TLS_DHE_RSA_WITH_AES_256_GCM_SHA384",
    ];

    // "Old" adds common ECDHE CBC suites. DockYarp floors at TLS 1.2, so it does not enable TLS 1.0/1.1.
    private static readonly ImmutableArray<string> OldSuites =
    [
        .. IntermediateSuites,
        "TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256",
        "TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256",
        "TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA384",
        "TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384",
    ];

    private static readonly Dictionary<string, SslPolicyResolution> Presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mozilla-Modern"] = new SslPolicyResolution(TlsVersion.Tls13, Tls13Suites),
        ["Mozilla-Intermediate"] = new SslPolicyResolution(TlsVersion.Tls12, IntermediateSuites),
        ["Mozilla-Old"] = new SslPolicyResolution(TlsVersion.Tls12, OldSuites),
    };

    /// <summary>Gets the names of the recognized <c>SSL_POLICY</c> presets.</summary>
    public static IReadOnlyCollection<string> KnownPresetNames { get; } = [.. Presets.Keys];

    /// <summary>Resolves the effective TLS version and cipher list, applying a named preset when recognized.</summary>
    /// <param name="policy">The configured <c>SSL_POLICY</c> name, or <see langword="null"/>.</param>
    /// <param name="configuredVersion">The explicitly configured minimum TLS version.</param>
    /// <param name="configuredCiphers">The explicitly configured cipher allow-list, or <see langword="null"/>.</param>
    /// <returns>The effective minimum version and cipher-suite names.</returns>
    public static SslPolicyResolution Resolve(
        string? policy, TlsVersion configuredVersion, IReadOnlyList<string>? configuredCiphers)
    {
        bool hasExplicitCiphers = configuredCiphers is { Count: > 0 };
        if (policy is { Length: > 0 } && Presets.TryGetValue(policy, out SslPolicyResolution preset))
        {
            // The preset sets the minimum version and default ciphers; an explicit cipher list still wins.
            return hasExplicitCiphers
                ? preset with { CipherSuites = [.. configuredCiphers!] }
                : preset;
        }

        return new SslPolicyResolution(
            configuredVersion, hasExplicitCiphers ? [.. configuredCiphers!] : []);
    }
}
