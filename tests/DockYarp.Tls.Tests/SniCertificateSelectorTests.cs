namespace DockYarp.Tls.Tests;

using System.IO.Abstractions.TestingHelpers;
using System.Security.Cryptography.X509Certificates;

using AwesomeAssertions;

using DockYarp.Core.Models;
using DockYarp.Core.Stores;
using DockYarp.Tls;

using Microsoft.Extensions.Logging.Abstractions;

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
        SniCertificateSelector selector = Selector(store, fallback);

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
        SniCertificateSelector selector = Selector(store, fallback);

        selector.Select("foo.example.com").Thumbprint.Should().Be(wildcard.Thumbprint);
    }

    /// <summary>The fallback certificate is returned for an unknown host or missing SNI.</summary>
    [Test]
    public void ReturnsFallbackWhenMissing()
    {
        FakeCertificateStore store = new();
        using DefaultCertificateProvider fallback = new(new TlsOptions(), new MockFileSystem());
        SniCertificateSelector selector = Selector(store, fallback);

        selector.Select("unknown.local").Thumbprint.Should().Be(fallback.Certificate.Thumbprint);
        selector.Select("bar.other.test").Thumbprint.Should().Be(fallback.Certificate.Thumbprint);
        selector.Select(null).Thumbprint.Should().Be(fallback.Certificate.Thumbprint);
    }

    /// <summary>A CERT_NAME pin overrides the per-host certificate with the shared one.</summary>
    [Test]
    public void SharedCertificatePinOverridesHostSelection()
    {
        FakeCertificateStore store = new();
        using X509Certificate2 hostCertificate = DefaultCertificateFactory.CreateSelfSigned("app.local");
        using X509Certificate2 sharedCertificate = DefaultCertificateFactory.CreateSelfSigned("shared");
        store.Save("app.local", hostCertificate);
        store.Save("shared", sharedCertificate);
        using DefaultCertificateProvider fallback = new(new TlsOptions(), new MockFileSystem());
        SniCertificateSelector selector = Selector(store, fallback, RouteWithCertName("app.local", "shared"));

        selector.Select("app.local").Thumbprint.Should().Be(sharedCertificate.Thumbprint);
    }

    /// <summary>A CERT_NAME pin whose certificate is missing falls back to per-host selection.</summary>
    [Test]
    public void MissingSharedCertificateFallsBackToHost()
    {
        FakeCertificateStore store = new();
        using X509Certificate2 hostCertificate = DefaultCertificateFactory.CreateSelfSigned("app.local");
        store.Save("app.local", hostCertificate);
        using DefaultCertificateProvider fallback = new(new TlsOptions(), new MockFileSystem());
        SniCertificateSelector selector = Selector(store, fallback, RouteWithCertName("app.local", "shared"));

        selector.Select("app.local").Thumbprint.Should().Be(hostCertificate.Thumbprint);
    }

    private static RouteRule RouteWithCertName(string host, string certName) =>
        new()
        {
            HostPattern = host,
            ClusterId = host,
            Tls = new HostTlsMetadata { CertificateHost = host, CertificateName = certName },
        };

    private static SniCertificateSelector Selector(
        FakeCertificateStore store, DefaultCertificateProvider fallback, params RouteRule[] routes)
    {
        RouteConfigStore routeStore = new();
        routeStore.Apply([.. routes], []);
        return new SniCertificateSelector(store, routeStore, fallback, NullLogger<SniCertificateSelector>.Instance);
    }
}
