namespace DockYarp.Tls.Tests;

using System.IO.Abstractions.TestingHelpers;
using System.Security.Cryptography.X509Certificates;

using AwesomeAssertions;

using DockYarp.Tls;

/// <summary>Tests for <see cref="SniCertificateSelector"/>.</summary>
public sealed class SniCertificateSelectorTests
{
    /// <summary>The host certificate is returned when present.</summary>
    [Test]
    public void ReturnsHostCertificateWhenPresent()
    {
        FakeCertificateStore store = new();
        using X509Certificate2 appCertificate = DefaultCertificateFactory.CreateSelfSigned("app.local");
        store.Save("app.local", appCertificate);
        using DefaultCertificateProvider fallback = new(new TlsOptions(), new MockFileSystem());
        SniCertificateSelector selector = new(store, fallback);

        X509Certificate2 selected = selector.Select("app.local");

        selected.Thumbprint.Should().Be(appCertificate.Thumbprint);
    }

    /// <summary>A subdomain with no exact certificate selects the parent-domain (wildcard) certificate.</summary>
    [Test]
    public void ReturnsParentDomainCertificateForSubdomain()
    {
        FakeCertificateStore store = new();
        using X509Certificate2 wildcard = DefaultCertificateFactory.CreateSelfSigned("*.example.com");
        store.Save("example.com", wildcard);
        using DefaultCertificateProvider fallback = new(new TlsOptions(), new MockFileSystem());
        SniCertificateSelector selector = new(store, fallback);

        selector.Select("foo.example.com").Thumbprint.Should().Be(wildcard.Thumbprint);
    }

    /// <summary>The fallback certificate is returned for an unknown host or missing SNI.</summary>
    [Test]
    public void ReturnsFallbackWhenMissing()
    {
        FakeCertificateStore store = new();
        using DefaultCertificateProvider fallback = new(new TlsOptions(), new MockFileSystem());
        SniCertificateSelector selector = new(store, fallback);

        selector.Select("unknown.local").Thumbprint.Should().Be(fallback.Certificate.Thumbprint);
        selector.Select("bar.other.test").Thumbprint.Should().Be(fallback.Certificate.Thumbprint);
        selector.Select(null).Thumbprint.Should().Be(fallback.Certificate.Thumbprint);
    }
}
