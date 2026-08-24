namespace DockYarp.Docker.Tests;

using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using AwesomeAssertions;

using DockYarp.Docker.Discovery;

using global::Docker.DotNet.Handler.Abstractions;

/// <summary>Tests for <see cref="DockerTlsCredentials"/> (client cert wiring + daemon verification).</summary>
public sealed class DockerTlsCredentialsTests
{
    private static readonly Uri TcpEndpoint = new("tcp://daemon:2376");
    private static readonly Uri SocketEndpoint = new("unix:///var/run/docker.sock");

    /// <summary>A non-tcp endpoint (or none) yields no TLS credentials — the connection is unchanged.</summary>
    [Test]
    public void NonTcpEndpointYieldsNoCredentials()
    {
        (string certPem, string keyPem) = SelfSignedPem("client");

        DockerTlsCredentials.Create(SocketEndpoint, DaemonTlsVerification.AcceptAny, null, certPem, keyPem)
            .Should().BeNull();
        DockerTlsCredentials.Create(null, DaemonTlsVerification.AcceptAny, null, certPem, keyPem)
            .Should().BeNull();
    }

    /// <summary>Without a complete client certificate the factory yields no credentials.</summary>
    [Test]
    public void MissingClientCertificateYieldsNoCredentials()
    {
        (string certPem, string keyPem) = SelfSignedPem("client");

        DockerTlsCredentials.Create(TcpEndpoint, DaemonTlsVerification.AcceptAny, null, null, keyPem)
            .Should().BeNull();
        DockerTlsCredentials.Create(TcpEndpoint, DaemonTlsVerification.AcceptAny, null, certPem, null)
            .Should().BeNull();
    }

    /// <summary>A tcp endpoint with a client cert wires the certificate and (AcceptAny) a permissive callback.</summary>
    [Test]
    public void TlsEndpointWiresClientCertificateAndAcceptsAnyDaemon()
    {
        (string certPem, string keyPem) = SelfSignedPem("client");
        IAuthProvider? credentials =
            DockerTlsCredentials.Create(TcpEndpoint, DaemonTlsVerification.AcceptAny, null, certPem, keyPem);

        credentials.Should().NotBeNull();
        credentials.TlsEnabled.Should().BeTrue();

        using SocketsHttpHandler handler = new();
        credentials.ConfigureHandler(handler);

        handler.SslOptions.ClientCertificates.Should().NotBeNull();
        handler.SslOptions.ClientCertificates.Count.Should().Be(1);
        handler.SslOptions.RemoteCertificateValidationCallback.Should().NotBeNull();

        using X509Certificate2 anyDaemon = SelfSignedCert("rogue");
        handler.SslOptions.RemoteCertificateValidationCallback(this, anyDaemon, null, SslPolicyErrors.RemoteCertificateChainErrors)
            .Should().BeTrue();
    }

    /// <summary>With verification the callback accepts a daemon cert chaining to the CA and rejects others.</summary>
    [Test]
    public void VerifyAgainstCaAcceptsCaSignedDaemonAndRejectsUnrelated()
    {
        using X509Certificate2 ca = CreateCa();
        (string clientCertPem, string clientKeyPem) = LeafPem(ca, "client");
        using X509Certificate2 daemon = LeafCert(ca, "daemon");
        using X509Certificate2 unrelated = SelfSignedCert("rogue");

        IAuthProvider? credentials = DockerTlsCredentials.Create(
            TcpEndpoint, DaemonTlsVerification.VerifyAgainstCa, ca.ExportCertificatePem(), clientCertPem, clientKeyPem);

        using SocketsHttpHandler handler = new();
        credentials!.ConfigureHandler(handler);
        RemoteCertificateValidationCallback callback = handler.SslOptions.RemoteCertificateValidationCallback!;

        callback(this, daemon, null, SslPolicyErrors.None).Should().BeTrue();
        callback(this, unrelated, null, SslPolicyErrors.None).Should().BeFalse();
    }

    private static X509Certificate2 CreateCa()
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new("CN=DockYarp Test CA", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    private static (string CertPem, string KeyPem) SelfSignedPem(string commonName)
    {
        using RSA rsa = RSA.Create(2048);
        using X509Certificate2 certificate = SelfSigned(rsa, commonName);
        return (certificate.ExportCertificatePem(), rsa.ExportPkcs8PrivateKeyPem());
    }

    private static (string CertPem, string KeyPem) LeafPem(X509Certificate2 issuer, string commonName)
    {
        using RSA rsa = RSA.Create(2048);
        using X509Certificate2 certificate = Leaf(rsa, issuer, commonName);
        return (certificate.ExportCertificatePem(), rsa.ExportPkcs8PrivateKeyPem());
    }

    private static X509Certificate2 SelfSignedCert(string commonName)
    {
        using RSA rsa = RSA.Create(2048);
        return SelfSigned(rsa, commonName);
    }

    private static X509Certificate2 LeafCert(X509Certificate2 issuer, string commonName)
    {
        using RSA rsa = RSA.Create(2048);
        return Leaf(rsa, issuer, commonName);
    }

    private static X509Certificate2 SelfSigned(RSA key, string commonName)
    {
        CertificateRequest request = new($"CN={commonName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddMonths(1));
    }

    private static X509Certificate2 Leaf(RSA key, X509Certificate2 issuer, string commonName)
    {
        CertificateRequest request = new($"CN={commonName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        return request.Create(
            issuer, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddMonths(1), RandomNumberGenerator.GetBytes(16));
    }
}
