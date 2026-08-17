namespace DockYarp.Tls.Tests;

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

/// <summary>Builds a real, throwaway leaf + intermediate certificate chain for chain-preservation tests.</summary>
internal static class TestChainFactory
{
    private const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";

    /// <summary>Creates a leaf certificate (with its private key) signed by a self-signed intermediate CA.</summary>
    /// <param name="commonName">The leaf certificate's common name / SAN.</param>
    /// <returns>The leaf (keyed) and the intermediate that signed it. The caller owns disposal of both.</returns>
    public static (X509Certificate2 Leaf, X509Certificate2 Intermediate) CreateChain(string commonName)
    {
        using RSA intermediateKey = RSA.Create(2048);
        CertificateRequest intermediateRequest = new(
            "CN=DockYarp Test Intermediate CA", intermediateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        intermediateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: true, hasPathLengthConstraint: true, pathLengthConstraint: 0, critical: true));
        intermediateRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));
        X509Certificate2 intermediate = intermediateRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2));

        using RSA leafKey = RSA.Create(2048);
        CertificateRequest leafRequest = new(
            $"CN={commonName}", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        leafRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(ServerAuthOid)], false));
        SubjectAlternativeNameBuilder subjectAlternativeName = new();
        subjectAlternativeName.AddDnsName(commonName);
        leafRequest.CertificateExtensions.Add(subjectAlternativeName.Build());

        using X509Certificate2 leafWithoutKey = leafRequest.Create(
            intermediate, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1), RandomNumberGenerator.GetBytes(16));
        X509Certificate2 leaf = leafWithoutKey.CopyWithPrivateKey(leafKey);

        return (leaf, intermediate);
    }
}
