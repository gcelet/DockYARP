namespace DockYarp.Tls.Tests;

using System;
using System.IO.Abstractions.TestingHelpers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using AwesomeAssertions;

using DockYarp.Tls;

/// <summary>Tests for <see cref="ClientCertificateValidator"/>.</summary>
public sealed class ClientCertificateValidatorTests
{
    // A fixed validity window shared by the CA and the issued client certificate. Capturing it once avoids a
    // race where two separate UtcNow.AddYears(1) calls straddle a second boundary, making the client's notAfter
    // exceed the issuer's and CertificateRequest.Create throw.
    private static readonly DateTimeOffset NotBefore = DateTimeOffset.UtcNow.AddDays(-1);
    private static readonly DateTimeOffset NotAfter = DateTimeOffset.UtcNow.AddYears(1);

    /// <summary>A certificate signed by the configured CA validates; an unrelated one does not.</summary>
    [Test]
    public void ValidatesAgainstConfiguredCa()
    {
        using RSA caKey = RSA.Create(2048);
        CertificateRequest caRequest = new("CN=Test CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using X509Certificate2 ca = caRequest.CreateSelfSigned(NotBefore, NotAfter);

        using X509Certificate2 client = IssueClientCertificate(ca);
        using X509Certificate2 unrelated = DefaultCertificateFactory.CreateSelfSigned("intruder.local");

        MockFileSystem fileSystem = new();
        string caPath = fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), "ca.crt");
        fileSystem.AddFile(caPath, new MockFileData(ca.ExportCertificatePem()));

        ClientCertificateValidator validator = new(
            new TlsOptions { ClientCaCertificatePath = caPath }, fileSystem);

        validator.HasClientCa.Should().BeTrue();
        validator.Validate(client).Should().BeTrue();
        validator.Validate(unrelated).Should().BeFalse();
    }

    /// <summary>With no CA configured, mutual TLS is disabled and validation fails closed.</summary>
    [Test]
    public void NoCaMeansNoClientAuth()
    {
        ClientCertificateValidator validator = new(new TlsOptions(), new MockFileSystem());

        validator.HasClientCa.Should().BeFalse();
        using X509Certificate2 any = DefaultCertificateFactory.CreateSelfSigned("app.local");
        validator.Validate(any).Should().BeFalse();
    }

    private static X509Certificate2 IssueClientCertificate(X509Certificate2 ca)
    {
        using RSA clientKey = RSA.Create(2048);
        CertificateRequest request = new("CN=client", clientKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

        // Same window as the CA (issuer), so the client's notAfter never exceeds the issuer's.
        return request.Create(ca, NotBefore, NotAfter, [1, 2, 3, 4]);
    }
}
