namespace DockYarp.Tls.Tests;

using System;
using System.IO.Abstractions.TestingHelpers;
using System.Security.Cryptography;

using AwesomeAssertions;

using DockYarp.Tls.Acme;

/// <summary>Tests for <see cref="AcmeAccountKeyStore"/>: load-or-generate, per-(email, endpoint) scoping, and
/// import-error reporting.</summary>
public sealed class AcmeAccountKeyStoreTests
{
    private static readonly Uri LetsEncryptDirectory = new("https://acme-v02.api.letsencrypt.org/directory");
    private static readonly Uri StepCaDirectory = new("https://stepca:9000/acme/acme/directory");

    /// <summary>No file present: a new EC P-256 key is generated and persisted at the resolved path.</summary>
    [Test]
    public void NoFilePresent_GeneratesAndPersistsNewKey()
    {
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);

        using ECDsa key = AcmeAccountKeyStore.LoadOrCreate(fileSystem, directory, LetsEncryptDirectory, "ops@example.com");

        key.KeySize.Should().Be(256);
        string path = AcmeAccountKeyStore.ResolvePath(fileSystem, directory, LetsEncryptDirectory, "ops@example.com");
        fileSystem.File.Exists(path).Should().BeTrue();
    }

    /// <summary>An existing EC PEM file's exact key material is returned unchanged, not regenerated.</summary>
    [Test]
    public void ExistingEcKey_IsReturnedUnchanged()
    {
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);

        using ECDsa first = AcmeAccountKeyStore.LoadOrCreate(fileSystem, directory, LetsEncryptDirectory, "ops@example.com");
        string firstThumbprint = AcmeJws.JwkThumbprint(first);

        using ECDsa second = AcmeAccountKeyStore.LoadOrCreate(fileSystem, directory, LetsEncryptDirectory, "ops@example.com");
        AcmeJws.JwkThumbprint(second).Should().Be(firstThumbprint, "the persisted key must be reused, not regenerated");
    }

    /// <summary>An operator-supplied EC key placed at the resolved path before first use (the migration/import
    /// path) is imported and reused as-is — DockYarp's own PKCS8 ("PRIVATE KEY") export form — not silently
    /// replaced by a freshly generated key.</summary>
    [Test]
    public void PreSeededEcKey_Pkcs8Form_IsImportedAsIs()
    {
        using ECDsa seeded = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        AssertPreSeededKeyIsImported(seeded, seeded.ExportPkcs8PrivateKeyPem());
    }

    /// <summary>Same as <see cref="PreSeededEcKey_Pkcs8Form_IsImportedAsIs"/>, but SEC1 ("EC PRIVATE KEY") —
    /// the form some acme.sh installations use — since import must accept either.</summary>
    [Test]
    public void PreSeededEcKey_Sec1Form_IsImportedAsIs()
    {
        using ECDsa seeded = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        AssertPreSeededKeyIsImported(seeded, seeded.ExportECPrivateKeyPem());
    }

    private static void AssertPreSeededKeyIsImported(ECDsa seeded, string pem)
    {
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        string path = AcmeAccountKeyStore.ResolvePath(fileSystem, directory, LetsEncryptDirectory, "ops@example.com");
        fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(path)!);
        fileSystem.File.WriteAllText(path, pem);
        string seededThumbprint = AcmeJws.JwkThumbprint(seeded);

        using ECDsa imported = AcmeAccountKeyStore.LoadOrCreate(fileSystem, directory, LetsEncryptDirectory, "ops@example.com");
        AcmeJws.JwkThumbprint(imported).Should().Be(seededThumbprint, "the pre-seeded key must be imported as-is, not regenerated");
    }

    /// <summary>An RSA PEM key at the resolved path fails with an error identifying it as RSA, not a generic parse failure.</summary>
    [Test]
    public void RsaKeyAtPath_FailsWithRsaSpecificError()
    {
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        string path = AcmeAccountKeyStore.ResolvePath(fileSystem, directory, LetsEncryptDirectory, "ops@example.com");
        fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(path)!);

        using RSA rsa = RSA.Create(2048);
        fileSystem.File.WriteAllText(path, rsa.ExportPkcs8PrivateKeyPem());

        Action act = () => AcmeAccountKeyStore.LoadOrCreate(fileSystem, directory, LetsEncryptDirectory, "ops@example.com");
        act.Should().Throw<InvalidOperationException>().WithMessage("*RSA*");
    }

    /// <summary>A corrupt/unparseable file fails with a generic unreadable-key error, not silently regenerating.</summary>
    [Test]
    public void CorruptFileAtPath_FailsWithUnreadableKeyError()
    {
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);
        string path = AcmeAccountKeyStore.ResolvePath(fileSystem, directory, LetsEncryptDirectory, "ops@example.com");
        fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(path)!);
        fileSystem.File.WriteAllText(path, "-----BEGIN PRIVATE KEY-----\nnot a real key\n-----END PRIVATE KEY-----");

        Action act = () => AcmeAccountKeyStore.LoadOrCreate(fileSystem, directory, LetsEncryptDirectory, "ops@example.com");
        act.Should().Throw<InvalidOperationException>().WithMessage("*could not be read*");
    }

    /// <summary>Two different ACME directory endpoints (same email) resolve to two distinct, independent keys.</summary>
    [Test]
    public void DifferentDirectoryEndpoints_ResolveToIndependentKeys()
    {
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);

        using ECDsa letsEncryptKey = AcmeAccountKeyStore.LoadOrCreate(fileSystem, directory, LetsEncryptDirectory, "ops@example.com");
        using ECDsa stepCaKey = AcmeAccountKeyStore.LoadOrCreate(fileSystem, directory, StepCaDirectory, "ops@example.com");

        AcmeJws.JwkThumbprint(stepCaKey).Should().NotBe(
            AcmeJws.JwkThumbprint(letsEncryptKey), "switching ACME endpoints must not reuse another endpoint's account key");

        // Switching endpoints must not disturb the key already persisted for the other one.
        using ECDsa letsEncryptKeyAgain = AcmeAccountKeyStore.LoadOrCreate(fileSystem, directory, LetsEncryptDirectory, "ops@example.com");
        AcmeJws.JwkThumbprint(letsEncryptKeyAgain).Should().Be(AcmeJws.JwkThumbprint(letsEncryptKey));
    }

    /// <summary>Two different contact emails (same endpoint) resolve to two distinct, independent keys — the
    /// specific regression email-scoping guards against (a per-host LETSENCRYPT_EMAIL must keep getting its
    /// own account, not be silently collapsed onto whichever email created the persisted account first).</summary>
    [Test]
    public void DifferentContactEmails_ResolveToIndependentKeys()
    {
        MockFileSystem fileSystem = new();
        string directory = CertificateDirectory(fileSystem);

        using ECDsa first = AcmeAccountKeyStore.LoadOrCreate(fileSystem, directory, LetsEncryptDirectory, "ops@example.com");
        using ECDsa second = AcmeAccountKeyStore.LoadOrCreate(fileSystem, directory, LetsEncryptDirectory, "other@example.com");

        AcmeJws.JwkThumbprint(second).Should().NotBe(AcmeJws.JwkThumbprint(first));
    }

    private static string CertificateDirectory(MockFileSystem fileSystem) =>
        fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), "certs");
}
