namespace DockYarp.Tls.Tests;

using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
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

            using (FileCertificateStore store = new(options, new FileSystem()))
            {
                store.Save("app.local", certificate);
                store.Find("app.local").Should().NotBeNull();
                store.List().Should().ContainSingle(info => info.Host == "app.local");
            }

            using FileCertificateStore reloaded = new(options, new FileSystem());
            reloaded.Find("app.local").Should().NotBeNull();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>A mounted PEM pair is loaded and served with a usable private key.</summary>
    [Test]
    public void PemPairIsLoaded()
    {
        using X509Certificate2 source = DefaultCertificateFactory.CreateSelfSigned("app.local");
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "app.local.crt"),
            new MockFileData(source.ExportCertificatePem()));
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "app.local.key"),
            new MockFileData(source.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem()));

        using FileCertificateStore store = new(new TlsOptions { CertificateDirectory = directory }, fileSystem);

        X509Certificate2? loaded = store.Find("app.local");
        loaded.Should().NotBeNull();
        loaded!.HasPrivateKey.Should().BeTrue();
    }

    /// <summary>A mounted PFX file is loaded.</summary>
    [Test]
    public void PfxFileIsLoaded()
    {
        using X509Certificate2 source = DefaultCertificateFactory.CreateSelfSigned("app.local");
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "app.local.pfx"),
            new MockFileData(source.Export(X509ContentType.Pfx)));

        using FileCertificateStore store = new(new TlsOptions { CertificateDirectory = directory }, fileSystem);

        store.Find("app.local").Should().NotBeNull();
    }

    /// <summary>A .crt without a matching .key is skipped.</summary>
    [Test]
    public void UnpairedCertificateIsSkipped()
    {
        using X509Certificate2 source = DefaultCertificateFactory.CreateSelfSigned("app.local");
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "app.local.crt"),
            new MockFileData(source.ExportCertificatePem()));

        using FileCertificateStore store = new(new TlsOptions { CertificateDirectory = directory }, fileSystem);

        store.Find("app.local").Should().BeNull();
    }

    private static string CertificateDirectory(MockFileSystem fileSystem) =>
        fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), "certs");

    /// <summary>The fallback certificate is generated with a usable private key.</summary>
    [Test]
    public void FallbackCertificateIsGenerated()
    {
        using X509Certificate2 fallback = DefaultCertificateFactory.CreateSelfSigned("dockyarp.local");

        fallback.Should().NotBeNull();
        fallback.HasPrivateKey.Should().BeTrue();
    }
}
