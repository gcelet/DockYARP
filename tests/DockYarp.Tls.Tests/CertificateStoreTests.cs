namespace DockYarp.Tls.Tests;

using System.IO;
using System.Security.Cryptography.X509Certificates;

using AwesomeAssertions;

using DockYarp.Tls;

/// <summary>Tests for <see cref="FileCertificateStore"/> and the fallback certificate.</summary>
public sealed class CertificateStoreTests
{
    /// <summary>A saved certificate is found and listed, and reloaded by a new store over the same directory.</summary>
    [Test]
    public void SavedCertificateIsFoundAndReloaded()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("dockyarp-certs");
        try
        {
            TlsOptions options = new() { CertificateDirectory = directory.FullName };
            using X509Certificate2 certificate = DefaultCertificateFactory.CreateSelfSigned("app.local");

            using (FileCertificateStore store = new(options))
            {
                store.Save("app.local", certificate);
                store.Find("app.local").Should().NotBeNull();
                store.List().Should().ContainSingle(info => info.Host == "app.local");
            }

            using FileCertificateStore reloaded = new(options);
            reloaded.Find("app.local").Should().NotBeNull();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>The fallback certificate is generated with a usable private key.</summary>
    [Test]
    public void FallbackCertificateIsGenerated()
    {
        using X509Certificate2 fallback = DefaultCertificateFactory.CreateSelfSigned("dockyarp.local");

        fallback.Should().NotBeNull();
        fallback.HasPrivateKey.Should().BeTrue();
    }
}
