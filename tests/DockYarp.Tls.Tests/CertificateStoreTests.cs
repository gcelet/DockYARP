namespace DockYarp.Tls.Tests;

using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Security.Cryptography;
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

    /// <summary>Save() persists a PEM pair (.crt/.key), not a PFX file, for an RSA-keyed certificate.</summary>
    [Test]
    public void SaveWritesRsaPemPair()
    {
        using X509Certificate2 certificate = DefaultCertificateFactory.CreateSelfSigned("pem-write-rsa.local");
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        using FileCertificateStore store = new(new TlsOptions { CertificateDirectory = directory }, fileSystem);

        store.Save("pem-write-rsa.local", new LoadedCertificate(certificate, []));

        string crtPath = fileSystem.Path.Combine(directory, "pem-write-rsa.local.crt");
        string keyPath = fileSystem.Path.Combine(directory, "pem-write-rsa.local.key");
        fileSystem.File.Exists(crtPath).Should().BeTrue();
        fileSystem.File.Exists(keyPath).Should().BeTrue();
        fileSystem.File.Exists(fileSystem.Path.Combine(directory, "pem-write-rsa.local.pfx")).Should().BeFalse();

        PemCertificateLoader.TryLoad(fileSystem, crtPath, keyPath, PrivateKeyPassphrases.None, out PemLoadResult reloadedResult).Should().BeTrue();
        LoadedCertificate reloaded = reloadedResult.Certificate;
        reloaded.Leaf.HasPrivateKey.Should().BeTrue();
        reloaded.Leaf.Thumbprint.Should().Be(certificate.Thumbprint);
    }

    /// <summary>Save() persists a usable PEM pair for an EC-keyed certificate too (the ACME path's key
    /// algorithm), not just RSA.</summary>
    [Test]
    public void SaveWritesEcPemPair()
    {
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CertificateRequest request = new("CN=pem-write-ec.local", ecdsa, HashAlgorithmName.SHA256);
        using X509Certificate2 certificate =
            request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        using FileCertificateStore store = new(new TlsOptions { CertificateDirectory = directory }, fileSystem);

        store.Save("pem-write-ec.local", new LoadedCertificate(certificate, []));

        string crtPath = fileSystem.Path.Combine(directory, "pem-write-ec.local.crt");
        string keyPath = fileSystem.Path.Combine(directory, "pem-write-ec.local.key");
        PemCertificateLoader.TryLoad(fileSystem, crtPath, keyPath, PrivateKeyPassphrases.None, out PemLoadResult reloadedResult).Should().BeTrue();
        LoadedCertificate reloaded = reloadedResult.Certificate;
        reloaded.Leaf.HasPrivateKey.Should().BeTrue();
        reloaded.Leaf.Thumbprint.Should().Be(certificate.Thumbprint);
    }

    /// <summary>Save() persists a full-chain .crt (leaf + intermediate) that round-trips intact through Load().</summary>
    [Test]
    public void SaveWritesFullChainPem()
    {
        (X509Certificate2 leaf, X509Certificate2 intermediate) = TestChainFactory.CreateChain("pem-write-chain.local");
        using X509Certificate2 disposableLeaf = leaf;
        using X509Certificate2 disposableIntermediate = intermediate;
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        using FileCertificateStore store = new(new TlsOptions { CertificateDirectory = directory }, fileSystem);

        store.Save("pem-write-chain.local", new LoadedCertificate(leaf, [intermediate]));

        string crtPath = fileSystem.Path.Combine(directory, "pem-write-chain.local.crt");
        string keyPath = fileSystem.Path.Combine(directory, "pem-write-chain.local.key");
        PemCertificateLoader.TryLoad(fileSystem, crtPath, keyPath, PrivateKeyPassphrases.None, out PemLoadResult reloadedResult).Should().BeTrue();
        LoadedCertificate reloaded = reloadedResult.Certificate;
        reloaded.Additional.Should().HaveCount(1, "the intermediate must round-trip through the written PEM file");
        ChainBuildsAgainst(reloaded, intermediate).Should().BeTrue();
    }

    /// <summary>Save() writes an encrypted private key when a passphrase is configured, and the store still
    /// serves it correctly (round-trips through a fresh store over the same directory).</summary>
    [Test]
    public void SaveEncryptsPrivateKeyWhenPassphraseConfigured()
    {
        using X509Certificate2 certificate = DefaultCertificateFactory.CreateSelfSigned("encrypted-write.local");
        string expectedThumbprint = certificate.Thumbprint; // captured before any store can dispose the certificate
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        TlsOptions options = new() { CertificateDirectory = directory, PrivateKeyEncryptionPassphrase = "correct horse" };

        using (FileCertificateStore store = new(options, fileSystem))
        {
            store.Save("encrypted-write.local", new LoadedCertificate(certificate, []));
        }

        string keyPath = fileSystem.Path.Combine(directory, "encrypted-write.local.key");
        fileSystem.File.ReadAllText(keyPath).Should().Contain("ENCRYPTED PRIVATE KEY");

        using FileCertificateStore reloaded = new(options, fileSystem);
        LoadedCertificate? loaded = reloaded.Find("encrypted-write.local");
        loaded.Should().NotBeNull();
        loaded.Leaf.Thumbprint.Should().Be(expectedThumbprint);
        loaded.Leaf.HasPrivateKey.Should().BeTrue();
    }

    /// <summary>An operator-provided plain (unencrypted) key still loads correctly even when a passphrase is
    /// configured — plain-vs-encrypted is decided from the PEM's own label, never from configuration.</summary>
    [Test]
    public void PlainKeyStillLoadsWhenPassphraseConfigured()
    {
        using X509Certificate2 source = DefaultCertificateFactory.CreateSelfSigned("plain-with-passphrase.local");
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "plain-with-passphrase.local.crt"),
            new MockFileData(source.ExportCertificatePem()));
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "plain-with-passphrase.local.key"),
            new MockFileData(source.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem()));

        using FileCertificateStore store = new(
            new TlsOptions { CertificateDirectory = directory, PrivateKeyEncryptionPassphrase = "some passphrase" },
            fileSystem);

        LoadedCertificate? loaded = store.Find("plain-with-passphrase.local");
        loaded.Should().NotBeNull();
        loaded.Leaf.HasPrivateKey.Should().BeTrue();
    }

    /// <summary>A key encrypted with the previous passphrase still loads via the rotation fallback.</summary>
    [Test]
    public void EncryptedKeyLoadsViaPreviousPassphraseFallback()
    {
        using X509Certificate2 source = DefaultCertificateFactory.CreateSelfSigned("rotated.local");
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        string oldPassphrase = "old passphrase";
        PbeParameters pbe = new(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, iterationCount: 1000);
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "rotated.local.crt"),
            new MockFileData(source.ExportCertificatePem()));
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "rotated.local.key"),
            new MockFileData(source.GetRSAPrivateKey()!.ExportEncryptedPkcs8PrivateKeyPem(oldPassphrase, pbe)));

        using FileCertificateStore store = new(
            new TlsOptions
            {
                CertificateDirectory = directory,
                PrivateKeyEncryptionPassphrase = "new passphrase",
                PreviousPrivateKeyEncryptionPassphrase = oldPassphrase,
            },
            fileSystem);

        LoadedCertificate? loaded = store.Find("rotated.local");
        loaded.Should().NotBeNull();
        loaded.Leaf.HasPrivateKey.Should().BeTrue();
    }

    /// <summary>An encrypted key that neither the current nor previous configured passphrase decrypts fails
    /// loudly at construction, not silently.</summary>
    [Test]
    public void UndecryptableEncryptedKeyFailsFast()
    {
        using X509Certificate2 source = DefaultCertificateFactory.CreateSelfSigned("undecryptable.local");
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        PbeParameters pbe = new(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, iterationCount: 1000);
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "undecryptable.local.crt"),
            new MockFileData(source.ExportCertificatePem()));
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "undecryptable.local.key"),
            new MockFileData(source.GetRSAPrivateKey()!.ExportEncryptedPkcs8PrivateKeyPem("actual passphrase", pbe)));

        TlsOptions options = new()
        {
            CertificateDirectory = directory,
            PrivateKeyEncryptionPassphrase = "wrong passphrase",
            PreviousPrivateKeyEncryptionPassphrase = "also wrong",
        };

        FluentActions.Invoking(() => new FileCertificateStore(options, fileSystem))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*undecryptable.local.key*");
    }

    /// <summary>No passphrase configured: the feature is fully off, so nothing is ever flagged as needing
    /// re-encryption, regardless of the key's own on-disk form.</summary>
    [Test]
    public void RequiresKeyReencryptionFalseWhenNoPassphraseConfigured()
    {
        using X509Certificate2 source = DefaultCertificateFactory.CreateSelfSigned("no-passphrase.local");
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "no-passphrase.local.crt"),
            new MockFileData(source.ExportCertificatePem()));
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "no-passphrase.local.key"),
            new MockFileData(source.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem()));

        using FileCertificateStore store = new(new TlsOptions { CertificateDirectory = directory }, fileSystem);

        store.RequiresKeyReencryption("no-passphrase.local").Should().BeFalse();
    }

    /// <summary>A plain key loaded while a passphrase is configured is flagged as needing re-encryption — the
    /// exact case the dashboard button/badge exists to surface (first-time enable).</summary>
    [Test]
    public void RequiresKeyReencryptionTrueForPlainKeyWhenPassphraseConfigured()
    {
        using X509Certificate2 source = DefaultCertificateFactory.CreateSelfSigned("plain-needs-reencrypt.local");
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "plain-needs-reencrypt.local.crt"),
            new MockFileData(source.ExportCertificatePem()));
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "plain-needs-reencrypt.local.key"),
            new MockFileData(source.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem()));

        using FileCertificateStore store = new(
            new TlsOptions { CertificateDirectory = directory, PrivateKeyEncryptionPassphrase = "some passphrase" },
            fileSystem);

        store.RequiresKeyReencryption("plain-needs-reencrypt.local").Should().BeTrue();
    }

    /// <summary>A key that only loaded via the previous-passphrase rotation fallback is flagged as needing
    /// re-encryption onto the new current passphrase.</summary>
    [Test]
    public void RequiresKeyReencryptionTrueForPreviousPassphraseFallback()
    {
        using X509Certificate2 source = DefaultCertificateFactory.CreateSelfSigned("rotated-needs-reencrypt.local");
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        string oldPassphrase = "old passphrase";
        PbeParameters pbe = new(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, iterationCount: 1000);
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "rotated-needs-reencrypt.local.crt"),
            new MockFileData(source.ExportCertificatePem()));
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "rotated-needs-reencrypt.local.key"),
            new MockFileData(source.GetRSAPrivateKey()!.ExportEncryptedPkcs8PrivateKeyPem(oldPassphrase, pbe)));

        using FileCertificateStore store = new(
            new TlsOptions
            {
                CertificateDirectory = directory,
                PrivateKeyEncryptionPassphrase = "new passphrase",
                PreviousPrivateKeyEncryptionPassphrase = oldPassphrase,
            },
            fileSystem);

        store.RequiresKeyReencryption("rotated-needs-reencrypt.local").Should().BeTrue();
    }

    /// <summary>A key already encrypted with the current passphrase is not flagged — nothing left to do.</summary>
    [Test]
    public void RequiresKeyReencryptionFalseWhenKeyAlreadyUnderCurrentPassphrase()
    {
        using X509Certificate2 source = DefaultCertificateFactory.CreateSelfSigned("already-current.local");
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        string passphrase = "current passphrase";
        PbeParameters pbe = new(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, iterationCount: 1000);
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "already-current.local.crt"),
            new MockFileData(source.ExportCertificatePem()));
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "already-current.local.key"),
            new MockFileData(source.GetRSAPrivateKey()!.ExportEncryptedPkcs8PrivateKeyPem(passphrase, pbe)));

        using FileCertificateStore store = new(
            new TlsOptions { CertificateDirectory = directory, PrivateKeyEncryptionPassphrase = passphrase },
            fileSystem);

        store.RequiresKeyReencryption("already-current.local").Should().BeFalse();
    }

    /// <summary>A PFX-backed host is never flagged, even with a passphrase configured — <see
    /// cref="FileCertificateStore.ConvertToPem"/> is the action for that host, and it already applies the
    /// current passphrase itself.</summary>
    [Test]
    public void RequiresKeyReencryptionFalseForPfxBackedHost()
    {
        using X509Certificate2 source = DefaultCertificateFactory.CreateSelfSigned("pfx-backed.local");
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "pfx-backed.local.pfx"),
            new MockFileData(source.Export(X509ContentType.Pfx)));

        using FileCertificateStore store = new(
            new TlsOptions { CertificateDirectory = directory, PrivateKeyEncryptionPassphrase = "some passphrase" },
            fileSystem);

        store.RequiresKeyReencryption("pfx-backed.local").Should().BeFalse();
    }

    /// <summary>ReencryptPrivateKey clears the flag: after rewriting the key under the current passphrase, the
    /// host no longer needs the action.</summary>
    [Test]
    public void ReencryptPrivateKeyClearsRequiresKeyReencryptionFlag()
    {
        using X509Certificate2 source = DefaultCertificateFactory.CreateSelfSigned("clears-flag.local");
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "clears-flag.local.crt"),
            new MockFileData(source.ExportCertificatePem()));
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "clears-flag.local.key"),
            new MockFileData(source.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem()));

        using FileCertificateStore store = new(
            new TlsOptions { CertificateDirectory = directory, PrivateKeyEncryptionPassphrase = "some passphrase" },
            fileSystem);
        store.RequiresKeyReencryption("clears-flag.local").Should().BeTrue();

        store.ReencryptPrivateKey("clears-flag.local").Should().BeTrue();

        store.RequiresKeyReencryption("clears-flag.local").Should().BeFalse();
        fileSystem.File.ReadAllText(fileSystem.Path.Combine(directory, "clears-flag.local.key")).Should().Contain("ENCRYPTED PRIVATE KEY");
    }

    /// <summary>A PEM pair wins over a legacy PFX for the same host, regardless of directory enumeration order.</summary>
    [Test]
    public void PemPairTakesPrecedenceOverPfxForSameHost()
    {
        using X509Certificate2 pemCertificate = DefaultCertificateFactory.CreateSelfSigned("precedence.local");
        using X509Certificate2 pfxCertificate = DefaultCertificateFactory.CreateSelfSigned("precedence.local");
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "precedence.local.crt"),
            new MockFileData(pemCertificate.ExportCertificatePem()));
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "precedence.local.key"),
            new MockFileData(pemCertificate.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem()));
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "precedence.local.pfx"),
            new MockFileData(pfxCertificate.Export(X509ContentType.Pfx)));

        using FileCertificateStore store = new(new TlsOptions { CertificateDirectory = directory }, fileSystem);

        LoadedCertificate? loaded = store.Find("precedence.local");
        loaded.Should().NotBeNull();
        loaded.Leaf.Thumbprint.Should().Be(pemCertificate.Thumbprint, "the PEM pair must win over a same-host legacy PFX");
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
        loaded.Leaf.HasPrivateKey.Should().BeTrue();
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

    /// <summary>IsPfxBacked reflects the live on-disk state: true for a PFX-only host, false once a PEM pair
    /// exists, false for a host with no certificate at all.</summary>
    [Test]
    public void IsPfxBackedReflectsDiskState()
    {
        using X509Certificate2 pfxOnly = DefaultCertificateFactory.CreateSelfSigned("pfx-only.local");
        using X509Certificate2 pemPair = DefaultCertificateFactory.CreateSelfSigned("pem-pair.local");
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "pfx-only.local.pfx"),
            new MockFileData(pfxOnly.Export(X509ContentType.Pfx)));
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "pem-pair.local.crt"),
            new MockFileData(pemPair.ExportCertificatePem()));
        fileSystem.AddFile(
            fileSystem.Path.Combine(directory, "pem-pair.local.key"),
            new MockFileData(pemPair.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem()));

        using FileCertificateStore store = new(new TlsOptions { CertificateDirectory = directory }, fileSystem);

        store.IsPfxBacked("pfx-only.local").Should().BeTrue();
        store.IsPfxBacked("pem-pair.local").Should().BeFalse();
        store.IsPfxBacked("unknown.local").Should().BeFalse();
    }

    /// <summary>ConvertToPem rewrites a PFX-backed host as PEM, removes the stale PFX, and leaves the
    /// certificate served correctly afterward — not disposed, same thumbprint (the exact regression a
    /// Save()-based implementation would risk).</summary>
    [Test]
    public void ConvertToPemRewritesPfxAsUsablePem()
    {
        using X509Certificate2 source = DefaultCertificateFactory.CreateSelfSigned("convert-me.local");
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        string pfxPath = fileSystem.Path.Combine(directory, "convert-me.local.pfx");
        fileSystem.AddFile(pfxPath, new MockFileData(source.Export(X509ContentType.Pfx)));
        using FileCertificateStore store = new(new TlsOptions { CertificateDirectory = directory }, fileSystem);
        LoadedCertificate? beforeConversion = store.Find("convert-me.local");
        beforeConversion.Should().NotBeNull();

        bool converted = store.ConvertToPem("convert-me.local");

        converted.Should().BeTrue();
        fileSystem.File.Exists(pfxPath).Should().BeFalse("the stale PFX must be removed after conversion");
        fileSystem.File.Exists(fileSystem.Path.Combine(directory, "convert-me.local.crt")).Should().BeTrue();
        fileSystem.File.Exists(fileSystem.Path.Combine(directory, "convert-me.local.key")).Should().BeTrue();
        store.IsPfxBacked("convert-me.local").Should().BeFalse();

        LoadedCertificate? afterConversion = store.Find("convert-me.local");
        afterConversion.Should().NotBeNull();
        ReferenceEquals(afterConversion, beforeConversion).Should().BeTrue(
            "ConvertToPem must not replace/dispose the in-memory certificate via Save()'s remove-then-dispose " +
            "path — the exact same object must still be served afterward");
        afterConversion.Leaf.Thumbprint.Should().Be(source.Thumbprint);
        afterConversion.Leaf.HasPrivateKey.Should().BeTrue();
    }

    /// <summary>Converting an unknown host is a no-op that returns false and touches no files.</summary>
    [Test]
    public void ConvertToPemOnUnknownHostReturnsFalse()
    {
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        using FileCertificateStore store = new(new TlsOptions { CertificateDirectory = directory }, fileSystem);

        store.ConvertToPem("unknown.local").Should().BeFalse();
        fileSystem.File.Exists(fileSystem.Path.Combine(directory, "unknown.local.crt")).Should().BeFalse();
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
        loaded.Leaf.HasPrivateKey.Should().BeTrue();
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
        loaded.Leaf.HasPrivateKey.Should().BeTrue();
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
