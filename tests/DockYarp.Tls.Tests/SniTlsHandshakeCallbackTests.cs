namespace DockYarp.Tls.Tests;

using System;
using System.IO.Abstractions.TestingHelpers;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using AwesomeAssertions;

using DockYarp.Core.Models;
using DockYarp.Core.Stores;
using DockYarp.Tls;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Tests for <see cref="SniTlsHandshakeCallback"/>.</summary>
public sealed class SniTlsHandshakeCallbackTests
{
    // A shared validity window for the CA and the issued client certificate (see ClientCertificateValidatorTests).
    private static readonly DateTimeOffset NotBefore = DateTimeOffset.UtcNow.AddDays(-1);
    private static readonly DateTimeOffset NotAfter = DateTimeOffset.UtcNow.AddYears(1);

    /// <summary>The SNI host certificate is served with the global protocol floor + ALPN, and no mutual TLS.</summary>
    [Test]
    public void BuildsHostCertificateWithGlobalPosture()
    {
        FakeCertificateStore store = new();
        using X509Certificate2 appCertificate = DefaultCertificateFactory.CreateSelfSigned("app.local");
        store.Save("app.local", appCertificate);
        using DefaultCertificateProvider fallback = new(new TlsOptions(), new MockFileSystem());
        SniTlsHandshakeCallback callback = Callback(store, fallback, new TlsOptions());

        SslServerAuthenticationOptions options = callback.BuildOptions("app.local");

        options.ServerCertificate.Should().BeSameAs(appCertificate);
        options.EnabledSslProtocols.Should().Be(SslProtocols.Tls12 | SslProtocols.Tls13);
        options.ApplicationProtocols.Should().Equal(SslApplicationProtocol.Http2, SslApplicationProtocol.Http11);
        options.ClientCertificateRequired.Should().BeFalse();
        options.RemoteCertificateValidationCallback.Should().BeNull();
    }

    /// <summary>An unknown SNI host falls back to the default certificate.</summary>
    [Test]
    public void UnknownHostUsesFallbackCertificate()
    {
        FakeCertificateStore store = new();
        using DefaultCertificateProvider fallback = new(new TlsOptions(), new MockFileSystem());
        SniTlsHandshakeCallback callback = Callback(store, fallback, new TlsOptions());

        SslServerAuthenticationOptions options = callback.BuildOptions("unknown.local");

        ((X509Certificate2)options.ServerCertificate!).Thumbprint.Should().Be(fallback.Certificate.Thumbprint);
    }

    /// <summary>A minimum of TLS 1.3 narrows the enabled protocols to TLS 1.3 only.</summary>
    [Test]
    public void MinimumTls13NarrowsProtocols()
    {
        FakeCertificateStore store = new();
        using DefaultCertificateProvider fallback = new(new TlsOptions(), new MockFileSystem());
        SniTlsHandshakeCallback callback =
            Callback(store, fallback, new TlsOptions { MinimumTlsVersion = TlsVersion.Tls13 });

        callback.BuildOptions("app.local").EnabledSslProtocols.Should().Be(SslProtocols.Tls13);
    }

    /// <summary>When a client CA is configured, mutual TLS is requested and the validation delegate enforces it.</summary>
    [Test]
    public void MutualTlsWiredWhenClientCaConfigured()
    {
        using X509Certificate2 ca = CreateCa();
        using X509Certificate2 client = IssueClientCertificate(ca);
        using X509Certificate2 unrelated = DefaultCertificateFactory.CreateSelfSigned("intruder.local");

        MockFileSystem fileSystem = new();
        string caPath = fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), "ca.crt");
        fileSystem.AddFile(caPath, new MockFileData(ca.ExportCertificatePem()));
        ClientCertificateValidator validator = new(new TlsOptions { ClientCaCertificatePath = caPath }, fileSystem);

        FakeCertificateStore store = new();
        using DefaultCertificateProvider fallback = new(new TlsOptions(), new MockFileSystem());
        SniTlsHandshakeCallback callback = Callback(store, fallback, new TlsOptions(), validator);

        SslServerAuthenticationOptions options = callback.BuildOptions("app.local");

        options.ClientCertificateRequired.Should().BeTrue();
        RemoteCertificateValidationCallback validate = options.RemoteCertificateValidationCallback!;
        validate.Should().NotBeNull();
        validate(this, null, null, SslPolicyErrors.RemoteCertificateNotAvailable).Should().BeTrue(); // no cert: allowed
        validate(this, client, null, SslPolicyErrors.None).Should().BeTrue(); // chains to the CA
        validate(this, unrelated, null, SslPolicyErrors.None).Should().BeFalse(); // does not chain
    }

    /// <summary>A host declaring an SSL_POLICY preset overrides the global posture; other hosts keep it.</summary>
    [Test]
    public void PerHostSslPolicyOverridesGlobalPosture()
    {
        FakeCertificateStore store = new();
        using DefaultCertificateProvider fallback = new(new TlsOptions(), new MockFileSystem());
        SniTlsHandshakeCallback callback =
            Callback(store, fallback, new TlsOptions(), routes: RoutesWithPolicy("modern.local", "Mozilla-Modern"));

        callback.BuildOptions("modern.local").EnabledSslProtocols.Should().Be(SslProtocols.Tls13);
        callback.BuildOptions("other.local").EnabledSslProtocols.Should().Be(SslProtocols.Tls12 | SslProtocols.Tls13);
    }

    /// <summary>An unrecognized per-host SSL_POLICY falls back to the global posture.</summary>
    [Test]
    public void UnknownPerHostSslPolicyFallsBackToGlobalPosture()
    {
        FakeCertificateStore store = new();
        using DefaultCertificateProvider fallback = new(new TlsOptions(), new MockFileSystem());
        SniTlsHandshakeCallback callback =
            Callback(store, fallback, new TlsOptions(), routes: RoutesWithPolicy("weird.local", "Bogus-Policy"));

        callback.BuildOptions("weird.local").EnabledSslProtocols.Should().Be(SslProtocols.Tls12 | SslProtocols.Tls13);
    }

    private static SniTlsHandshakeCallback Callback(
        FakeCertificateStore store,
        DefaultCertificateProvider fallback,
        TlsOptions options,
        ClientCertificateValidator? clientCertificates = null,
        RouteConfigStore? routes = null)
    {
        RouteConfigStore routeStore = routes ?? new RouteConfigStore();
        SniCertificateSelector selector =
            new(store, routeStore, fallback, NullLogger<SniCertificateSelector>.Instance);
        return new SniTlsHandshakeCallback(
            selector,
            routeStore,
            clientCertificates ?? new ClientCertificateValidator(new TlsOptions(), new MockFileSystem()),
            options,
            NullLogger<SniTlsHandshakeCallback>.Instance);
    }

    private static RouteConfigStore RoutesWithPolicy(string host, string policy)
    {
        RouteConfigStore store = new();
        store.Apply(
            [new RouteRule { HostPattern = host, ClusterId = host, Tls = new HostTlsMetadata { CertificateHost = host, SslPolicy = policy } }],
            []);
        return store;
    }

    private static X509Certificate2 CreateCa()
    {
        using RSA caKey = RSA.Create(2048);
        CertificateRequest request = new("CN=Test CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        return request.CreateSelfSigned(NotBefore, NotAfter);
    }

    private static X509Certificate2 IssueClientCertificate(X509Certificate2 ca)
    {
        using RSA clientKey = RSA.Create(2048);
        CertificateRequest request = new("CN=client", clientKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        return request.Create(ca, NotBefore, NotAfter, [1, 2, 3, 4]);
    }
}
