namespace DockYarp.Security.Tests;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using AwesomeAssertions;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// DockYarp's options type collides by simple name with Microsoft.AspNetCore.DataProtection.DataProtectionOptions
// (imported above for IDataProtectionProvider), so alias the DockYarp one explicitly.
using DpOptions = DockYarp.Security.DataProtectionOptions;

/// <summary>Tests for <see cref="DataProtectionSetup"/>: certificate loading and the encrypt-vs-suppress decision.</summary>
public sealed class DataProtectionSetupTests
{
    private const string Password = "pfx-pass";

    private string workDir = string.Empty;

    /// <summary>Creates a unique working directory for key rings and certificate files.</summary>
    [SetUp]
    public void SetUp() =>
        workDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "dockyarp-dp-tests", Guid.NewGuid().ToString("N"))).FullName;

    /// <summary>Removes the working directory.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(workDir))
        {
            Directory.Delete(workDir, recursive: true);
        }
    }

    /// <summary>No configured path yields no certificate (default, unencrypted path).</summary>
    [Test]
    public void LoadEncryptionCertificateReturnsNullWhenNoPathConfigured()
    {
        DataProtectionSetup.LoadEncryptionCertificate(new DpOptions()).Should().BeNull();
    }

    /// <summary>A valid PFX is loaded with its private key and matching thumbprint.</summary>
    [Test]
    public void LoadEncryptionCertificateLoadsValidPfx()
    {
        (string path, string expectedThumbprint) = WritePfx();
        DpOptions options = new() { CertificatePath = path, CertificatePassword = Password };

        using X509Certificate2? certificate = DataProtectionSetup.LoadEncryptionCertificate(options);

        certificate.Should().NotBeNull();
        certificate.Thumbprint.Should().Be(expectedThumbprint);
        certificate.HasPrivateKey.Should().BeTrue();
    }

    /// <summary>A missing certificate file fails with an actionable error naming the path.</summary>
    [Test]
    public void LoadEncryptionCertificateThrowsWhenFileMissing()
    {
        string missing = Path.Combine(workDir, "does-not-exist.pfx");
        DpOptions options = new() { CertificatePath = missing };

        Action act = () => DataProtectionSetup.LoadEncryptionCertificate(options);

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{missing}*");
    }

    /// <summary>A wrong password fails with an actionable error rather than a raw crypto exception.</summary>
    [Test]
    public void LoadEncryptionCertificateThrowsWhenPasswordWrong()
    {
        (string path, _) = WritePfx();
        DpOptions options = new() { CertificatePath = path, CertificatePassword = "wrong" };

        Action act = () => DataProtectionSetup.LoadEncryptionCertificate(options);

        act.Should().Throw<InvalidOperationException>().WithInnerException<CryptographicException>();
    }

    /// <summary>With no certificate, the key ring is persisted unencrypted and the warning is suppressed.</summary>
    [Test]
    public void AddDockYarpDataProtectionSuppressesWhenNoCertificate()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        DataProtectionKeyEncryption result = builder.AddDockYarpDataProtection(new DpOptions(), workDir);

        result.Should().Be(DataProtectionKeyEncryption.SuppressedUnencrypted);
    }

    /// <summary>With a certificate, the key ring is encrypted and a protect/unprotect round-trip succeeds.</summary>
    [Test]
    public void AddDockYarpDataProtectionEncryptsAndRoundTripsWithCertificate()
    {
        (string path, _) = WritePfx();
        DpOptions options = new() { CertificatePath = path, CertificatePassword = Password };
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        DataProtectionKeyEncryption result = builder.AddDockYarpDataProtection(options, workDir);
        result.Should().Be(DataProtectionKeyEncryption.Encrypted);

        using IHost host = builder.Build();
        IDataProtector protector = host.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("test");

        protector.Unprotect(protector.Protect("payload")).Should().Be("payload");
    }

    // Writes a self-signed PFX (with private key) into the working directory; returns its path and thumbprint.
    private (string Path, string Thumbprint) WritePfx()
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new(
            "CN=DockYarp DP Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        string path = Path.Combine(workDir, "dp.pfx");
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pkcs12, Password));
        return (path, certificate.Thumbprint);
    }
}
