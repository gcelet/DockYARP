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

    /// <summary>The TLS-1.3-only ELB policy selects TLS 1.3 with the TLS 1.3 suites.</summary>
    [Test]
    public void ElbTls13OnlySelectsTls13()
    {
        SslPolicyResolution resolved =
            SslPolicyPresets.Resolve("ELBSecurityPolicy-TLS13-1-3-2021-06", TlsVersion.Tls12, null);

        resolved.MinimumTlsVersion.Should().Be(TlsVersion.Tls13);
        resolved.CipherSuites.Should().BeEquivalentTo(
            "TLS_AES_128_GCM_SHA256", "TLS_AES_256_GCM_SHA384", "TLS_CHACHA20_POLY1305_SHA256");
    }

    /// <summary>A restricted 1.2 ELB policy clamps to TLS 1.2 with the intermediate (GCM/FS) suites.</summary>
    [Test]
    public void ElbRestricted12SelectsTls12Intermediate()
    {
        SslPolicyResolution resolved =
            SslPolicyPresets.Resolve("ELBSecurityPolicy-FS-1-2-Res-2020-10", TlsVersion.Tls13, null);

        resolved.MinimumTlsVersion.Should().Be(TlsVersion.Tls12);
        resolved.CipherSuites.Should().Contain("TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256");
    }

    /// <summary>A broad ELB policy clamps to the TLS 1.2 floor with the old (CBC-including) suites (case-insensitive).</summary>
    [Test]
    public void ElbBroadPolicyClampsToTls12Old()
    {
        SslPolicyResolution resolved =
            SslPolicyPresets.Resolve("elbsecuritypolicy-2016-08", TlsVersion.Tls13, null);

        resolved.MinimumTlsVersion.Should().Be(TlsVersion.Tls12);
        resolved.CipherSuites.Should().Contain("TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256");
    }

    /// <summary>A specialized FIPS ELB variant is not recognized and falls back to the configured values.</summary>
    [Test]
    public void ElbFipsVariantFallsBack()
    {
        SslPolicyResolution resolved =
            SslPolicyPresets.Resolve("ELBSecurityPolicy-TLS13-1-2-FIPS-2023-04", TlsVersion.Tls12, null);

        resolved.MinimumTlsVersion.Should().Be(TlsVersion.Tls12);
        resolved.CipherSuites.Should().BeEmpty();
    }

    /// <summary>An explicit cipher list still overrides an ELB preset's ciphers.</summary>
    [Test]
    public void ExplicitCiphersOverrideElbPreset()
    {
        ImmutableArray<string> explicitCiphers = ["TLS_AES_256_GCM_SHA384"];

        SslPolicyResolution resolved =
            SslPolicyPresets.Resolve("ELBSecurityPolicy-TLS13-1-2-2021-06", TlsVersion.Tls12, explicitCiphers);

        resolved.MinimumTlsVersion.Should().Be(TlsVersion.Tls12);
        resolved.CipherSuites.Should().ContainSingle().Which.Should().Be("TLS_AES_256_GCM_SHA384");
    }
}
