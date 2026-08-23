namespace DockYarp.Tls.Tests;

using System;
using System.IO.Abstractions.TestingHelpers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using AwesomeAssertions;

using DockYarp.Tls;

using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

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

    /// <summary>A certificate whose serial number is listed in the configured CRL is rejected, even though it
    /// chains to the CA; an unlisted certificate from the same CA still validates.</summary>
    [Test]
    public void RevokedCertificateFailsValidation()
    {
        using RSA caKey = RSA.Create(2048);
        CertificateRequest caRequest = new("CN=Test CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using X509Certificate2 ca = caRequest.CreateSelfSigned(NotBefore, NotAfter);

        using X509Certificate2 revoked = IssueClientCertificate(ca, [5, 6, 7, 8]);
        using X509Certificate2 clean = IssueClientCertificate(ca, [9, 10, 11, 12]);

        MockFileSystem fileSystem = new();
        string caPath = fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), "ca.crt");
        fileSystem.AddFile(caPath, new MockFileData(ca.ExportCertificatePem()));
        string crlPath = fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), "ca.crl");
        fileSystem.AddFile(crlPath, new MockFileData(BuildCrl(ca, caKey, revoked.SerialNumber)));

        ClientCertificateValidator validator = new(
            new TlsOptions { ClientCaCertificatePath = caPath, ClientCrlPath = crlPath }, fileSystem);

        validator.Validate(revoked).Should().BeFalse();
        validator.Validate(clean).Should().BeTrue();
    }

    private static X509Certificate2 IssueClientCertificate(X509Certificate2 ca) => IssueClientCertificate(ca, [1, 2, 3, 4]);

    private static X509Certificate2 IssueClientCertificate(X509Certificate2 ca, byte[] serialNumber)
    {
        using RSA clientKey = RSA.Create(2048);
        CertificateRequest request = new("CN=client", clientKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

        // Same window as the CA (issuer), so the client's notAfter never exceeds the issuer's.
        return request.Create(ca, NotBefore, NotAfter, serialNumber);
    }

    // Builds a real, BouncyCastle-signed CRL revoking one serial number — a fixture generated in the test
    // itself, not an opaque pre-baked file (see the change's design.md: BouncyCastle covers both parsing AND
    // generating a CRL, avoiding a static file that can't vary per test case).
    private static byte[] BuildCrl(X509Certificate2 ca, RSA caKey, string revokedSerialHex)
    {
        AsymmetricCipherKeyPair caKeyPair = DotNetUtilities.GetRsaKeyPair(caKey);
        X509V2CrlGenerator generator = new();
        generator.SetIssuerDN(new X509Name(ca.Subject));
        generator.SetThisUpdate(NotBefore.UtcDateTime);
        generator.SetNextUpdate(NotAfter.UtcDateTime);
        generator.AddCrlEntry(new BigInteger(revokedSerialHex, 16), DateTime.UtcNow, 0);

        Asn1SignatureFactory signatureFactory = new("SHA256WITHRSA", caKeyPair.Private);
        return generator.Generate(signatureFactory).GetEncoded();
    }
}
