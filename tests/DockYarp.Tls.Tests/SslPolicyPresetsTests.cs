namespace DockYarp.Tls.Tests;

using System.Collections.Immutable;

using AwesomeAssertions;

using DockYarp.Tls;

/// <summary>Tests for <see cref="SslPolicyPresets"/>.</summary>
public sealed class SslPolicyPresetsTests
{
    /// <summary>The Modern preset selects TLS 1.3 and the TLS 1.3 suites.</summary>
    [Test]
    public void ModernSelectsTls13()
    {
        SslPolicyResolution resolved = SslPolicyPresets.Resolve("Mozilla-Modern", TlsVersion.Tls12, null);

        resolved.MinimumTlsVersion.Should().Be(TlsVersion.Tls13);
        resolved.CipherSuites.Should().BeEquivalentTo(
            "TLS_AES_128_GCM_SHA256", "TLS_AES_256_GCM_SHA384", "TLS_CHACHA20_POLY1305_SHA256");
    }

    /// <summary>The Intermediate preset selects TLS 1.2 and includes ECDHE GCM suites (case-insensitive name).</summary>
    [Test]
    public void IntermediateSelectsTls12WithEcdheSuites()
    {
        SslPolicyResolution resolved = SslPolicyPresets.Resolve("mozilla-intermediate", TlsVersion.Tls13, null);

        resolved.MinimumTlsVersion.Should().Be(TlsVersion.Tls12);
        resolved.CipherSuites.Should().Contain("TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256");
        resolved.CipherSuites.Should().Contain("TLS_AES_128_GCM_SHA256");
    }

    /// <summary>An explicit cipher list overrides the preset's ciphers (but keeps the preset version).</summary>
    [Test]
    public void ExplicitCiphersOverridePreset()
    {
        ImmutableArray<string> explicitCiphers = ["TLS_AES_256_GCM_SHA384"];

        SslPolicyResolution resolved =
            SslPolicyPresets.Resolve("Mozilla-Intermediate", TlsVersion.Tls12, explicitCiphers);

        resolved.MinimumTlsVersion.Should().Be(TlsVersion.Tls12);
        resolved.CipherSuites.Should().ContainSingle().Which.Should().Be("TLS_AES_256_GCM_SHA384");
    }

    /// <summary>An unset or unknown policy returns the configured values unchanged.</summary>
    [Test]
    public void UnknownPolicyFallsBackToConfigured()
    {
        SslPolicyResolution unset = SslPolicyPresets.Resolve(null, TlsVersion.Tls13, null);
        unset.MinimumTlsVersion.Should().Be(TlsVersion.Tls13);
        unset.CipherSuites.Should().BeEmpty();

        ImmutableArray<string> configured = ["TLS_AES_128_GCM_SHA256"];
        SslPolicyResolution unknown = SslPolicyPresets.Resolve("Bogus", TlsVersion.Tls12, configured);
        unknown.MinimumTlsVersion.Should().Be(TlsVersion.Tls12);
        unknown.CipherSuites.Should().ContainSingle().Which.Should().Be("TLS_AES_128_GCM_SHA256");
    }
}
