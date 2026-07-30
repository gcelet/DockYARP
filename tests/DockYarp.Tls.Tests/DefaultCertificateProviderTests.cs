namespace DockYarp.Tls.Tests;

using System.IO.Abstractions.TestingHelpers;
using System.Security.Cryptography.X509Certificates;

using AwesomeAssertions;

using DockYarp.Tls;

/// <summary>Tests for <see cref="DefaultCertificateProvider"/>.</summary>
public sealed class DefaultCertificateProviderTests
{
    /// <summary>An operator-supplied default.crt/.key is preferred over the self-signed certificate.</summary>
    [Test]
    public void PrefersOperatorDefaultCertificate()
    {
        using X509Certificate2 operatorCert = DefaultCertificateFactory.CreateSelfSigned("operator.example");
        MockFileSystem fileSystem = new();
        fileSystem.AddFile("certs/default.crt", new MockFileData(operatorCert.ExportCertificatePem()));
        fileSystem.AddFile("certs/default.key", new MockFileData(operatorCert.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem()));

        using DefaultCertificateProvider provider = new(new TlsOptions { CertificateDirectory = "certs" }, fileSystem);

        provider.Certificate.Thumbprint.Should().Be(operatorCert.Thumbprint);
    }

    /// <summary>Without an operator default certificate, a self-signed certificate is generated.</summary>
    [Test]
    public void FallsBackToSelfSignedWhenNoOperatorCertificate()
    {
        using DefaultCertificateProvider provider = new(new TlsOptions { CertificateDirectory = "certs" }, new MockFileSystem());

        provider.Certificate.Subject.Should().Contain("dockyarp.local");
    }
}
