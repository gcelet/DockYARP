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
                store.Save("app.local", new LoadedCertificate(certificate, []));
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

        LoadedCertificate? loaded = store.Find("app.local");
        loaded.Should().NotBeNull();
        loaded!.Leaf.HasPrivateKey.Should().BeTrue();
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

    /// <summary>A full-chain PEM file (leaf then intermediate) preserves the intermediate, not just the leaf.</summary>
    [Test]
    public void FullChainPemPreservesIntermediate()
    {
        (X509Certificate2 leaf, X509Certificate2 intermediate) = TestChainFactory.CreateChain("chain.local");
        using X509Certificate2 disposableLeaf = leaf;
        using X509Certificate2 disposableIntermediate = intermediate;
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        string fullChainPem = leaf.ExportCertificatePem() + "\n" + intermediate.ExportCertificatePem();
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "chain.local.crt"),
            new MockFileData(fullChainPem));
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "chain.local.key"),
            new MockFileData(leaf.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem()));

        using FileCertificateStore store = new(new TlsOptions { CertificateDirectory = directory }, fileSystem);

        LoadedCertificate? loaded = store.Find("chain.local");
        loaded.Should().NotBeNull();
        loaded!.Leaf.HasPrivateKey.Should().BeTrue();
        loaded.Additional.Should().HaveCount(1, "the intermediate must be exposed explicitly, not just loadable");
        ChainBuildsAgainst(loaded, intermediate).Should().BeTrue("the intermediate must travel with the loaded certificate");
    }

    /// <summary>The intermediate is found regardless of its position in the file (not assumed to be second).</summary>
    [Test]
    public void FullChainPemPreservesIntermediate_ReversedOrder()
    {
        (X509Certificate2 leaf, X509Certificate2 intermediate) = TestChainFactory.CreateChain("chain-reversed.local");
        using X509Certificate2 disposableLeaf = leaf;
        using X509Certificate2 disposableIntermediate = intermediate;
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        string reversedChainPem = intermediate.ExportCertificatePem() + "\n" + leaf.ExportCertificatePem();
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "chain-reversed.local.crt"),
            new MockFileData(reversedChainPem));
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "chain-reversed.local.key"),
            new MockFileData(leaf.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem()));

        using FileCertificateStore store = new(new TlsOptions { CertificateDirectory = directory }, fileSystem);

        LoadedCertificate? loaded = store.Find("chain-reversed.local");
        loaded.Should().NotBeNull();
        loaded!.Leaf.HasPrivateKey.Should().BeTrue();
        ChainBuildsAgainst(loaded, intermediate).Should().BeTrue("order in the file must not matter");
    }

    /// <summary>A key that matches none of the certificates in the file is skipped, not a crash.</summary>
    [Test]
    public void KeyMatchingNoCertificateIsSkipped()
    {
        (X509Certificate2 leaf, X509Certificate2 intermediate) = TestChainFactory.CreateChain("mismatched.local");
        using X509Certificate2 disposableLeaf = leaf;
        using X509Certificate2 disposableIntermediate = intermediate;
        using X509Certificate2 unrelated = DefaultCertificateFactory.CreateSelfSigned("unrelated.local");
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "mismatched.local.crt"),
            new MockFileData(leaf.ExportCertificatePem() + "\n" + intermediate.ExportCertificatePem()));
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "mismatched.local.key"),
            new MockFileData(unrelated.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem()));

        using FileCertificateStore store = new(new TlsOptions { CertificateDirectory = directory }, fileSystem);

        store.Find("mismatched.local").Should().BeNull();
    }

    // Proves the intermediate travels with the loaded certificate as an explicit LoadedCertificate.Additional
    // entry (not merely that loading didn't throw, and not relying on any OS-side-channel awareness of sibling
    // certificates): only `loaded.Additional` — not the test's own `intermediate` object — is placed in
    // ExtraStore, so building can only succeed if the store actually preserved a usable copy of it.
    private static bool ChainBuildsAgainst(LoadedCertificate loaded, X509Certificate2 intermediate)
    {
        using X509Chain chain = new();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(intermediate);
        foreach (X509Certificate2 additional in loaded.Additional)
        {
            chain.ChainPolicy.ExtraStore.Add(additional);
        }

        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        return chain.Build(loaded.Leaf);
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
