namespace DockYarp.Tls.Tests;

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using AwesomeAssertions;

using Certes;
using Certes.Acme;

using DockYarp.Tls;

/// <summary>Tests for <see cref="CertesAcmeClient"/>'s chain assembly — the only part of the class that does
/// not require a live ACME network round-trip.</summary>
public sealed class CertesAcmeClientTests
{
    /// <summary>A private CA following normal ACME convention (root trusted out of band, never sent in the
    /// response) still yields a certificate carrying its intermediate — the real bug this change fixes.</summary>
    [Test]
    public void ChainWithoutRootInResponsePreservesIntermediate()
    {
        IKey leafKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
        using ThreeTierChain fixture = CreateThreeTierChain(leafKey);

        // No root in the response — the shape a private CA following normal ACME convention returns.
        CertificateChain chain = new(fixture.Leaf.ExportCertificatePem() + "\n" + fixture.Intermediate.ExportCertificatePem());

        LoadedCertificate loaded = CertesAcmeClient.BuildLoadedCertificate(chain, leafKey);
        try
        {
            loaded.Leaf.HasPrivateKey.Should().BeTrue();
            loaded.Additional.Should().HaveCount(1, "the intermediate must be preserved even though no root was returned");
            ChainBuildsAgainst(loaded, fixture.Root).Should().BeTrue("the preserved intermediate must actually complete a path to the root");
        }
        finally
        {
            DisposeLoaded(loaded);
        }
    }

    /// <summary>A CA that does bundle its root does not regress — the intermediate is still preserved.</summary>
    [Test]
    public void ChainWithRootInResponseStillPreservesIntermediate()
    {
        IKey leafKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
        using ThreeTierChain fixture = CreateThreeTierChain(leafKey);

        CertificateChain chain = new(
            fixture.Leaf.ExportCertificatePem() + "\n" + fixture.Intermediate.ExportCertificatePem() + "\n"
            + fixture.Root.ExportCertificatePem());

        LoadedCertificate loaded = CertesAcmeClient.BuildLoadedCertificate(chain, leafKey);
        try
        {
            loaded.Leaf.HasPrivateKey.Should().BeTrue();
            loaded.Additional.Should().Contain(
                certificate => certificate.Thumbprint == fixture.Intermediate.Thumbprint,
                "the intermediate must still be preserved when a root happens to be present too");
            ChainBuildsAgainst(loaded, fixture.Root).Should().BeTrue();
        }
        finally
        {
            DisposeLoaded(loaded);
        }
    }

    // Proves the intermediate travels with the loaded certificate, not merely that assembly didn't throw:
    // builds a chain trusting only `root` (CustomRootTrust) with `loaded.Additional` — not any object the CA
    // fixture built directly — supplying candidates in ExtraStore.
    private static bool ChainBuildsAgainst(LoadedCertificate loaded, X509Certificate2 root)
    {
        using X509Chain chain = new();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        foreach (X509Certificate2 additional in loaded.Additional)
        {
            chain.ChainPolicy.ExtraStore.Add(additional);
        }

        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        return chain.Build(loaded.Leaf);
    }

    private static void DisposeLoaded(LoadedCertificate loaded)
    {
        loaded.Leaf.Dispose();
        foreach (X509Certificate2 additional in loaded.Additional)
        {
            additional.Dispose();
        }
    }

    // A genuine three-tier hierarchy (self-signed root -> intermediate -> leaf), unlike TestChainFactory's
    // two-tier leaf+self-signed-CA shape — needed here to distinguish "root present in the response" from
    // "root absent," which is exactly what this bug depends on.
    private static ThreeTierChain CreateThreeTierChain(IKey leafKey)
    {
        DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-1);

        // EC throughout (matching the leaf's ES256 key) — CertificateRequest.Create(issuerCertificate, ...)
        // requires the issuer's key algorithm to match the request's own, unless using the
        // X509SignatureGenerator overload for cross-algorithm signing; EC avoids that complexity here.
        using ECDsa rootKey = ECDsa.Create();
        CertificateRequest rootRequest = new(
            "CN=DockYarp Test Root CA", rootKey, HashAlgorithmName.SHA256);
        rootRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        rootRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));
        X509Certificate2 root = rootRequest.CreateSelfSigned(from, from.AddYears(5));

        using ECDsa intermediateKey = ECDsa.Create();
        CertificateRequest intermediateRequest = new(
            "CN=DockYarp Test Intermediate CA", intermediateKey, HashAlgorithmName.SHA256);
        intermediateRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: true, pathLengthConstraint: 0, critical: true));
        intermediateRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));
        using X509Certificate2 intermediateWithoutKey = intermediateRequest.Create(
            root, from, from.AddYears(2), RandomNumberGenerator.GetBytes(16));
        X509Certificate2 intermediate = intermediateWithoutKey.CopyWithPrivateKey(intermediateKey);

        using ECDsa leafPublicKey = ECDsa.Create();
        leafPublicKey.ImportFromPem(leafKey.ToPem());
        CertificateRequest leafRequest = new(
            "CN=acme.example.local", leafPublicKey, HashAlgorithmName.SHA256);
        leafRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: false));
        X509Certificate2 leaf = leafRequest.Create(intermediate, from, from.AddYears(1), RandomNumberGenerator.GetBytes(16));

        return new ThreeTierChain(leaf, intermediate, root);
    }

    /// <summary>A self-signed root, an intermediate it signed, and a leaf the intermediate signed.</summary>
    private sealed record ThreeTierChain(X509Certificate2 Leaf, X509Certificate2 Intermediate, X509Certificate2 Root) : IDisposable
    {
        public void Dispose()
        {
            Leaf.Dispose();
            Intermediate.Dispose();
            Root.Dispose();
        }
    }
}
