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
    /// <summary>A certificate signed by the configured CA validates; an unrelated one does not.</summary>
    [Test]
    public void ValidatesAgainstConfiguredCa()
    {
        using RSA caKey = RSA.Create(2048);
        CertificateRequest caRequest = new("CN=Test CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using X509Certificate2 ca = caRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

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
        return request.Create(ca, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1), [1, 2, 3, 4]);
    }
}
