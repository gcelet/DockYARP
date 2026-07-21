namespace DockYarp.Tls;

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

/// <summary>Creates self-signed certificates used as the SNI fallback.</summary>
public static class DefaultCertificateFactory
{
    private const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";

    /// <summary>Creates a self-signed certificate for the given common name.</summary>
    /// <param name="commonName">The certificate common name / SAN.</param>
    /// <returns>A self-signed certificate valid for one year.</returns>
    public static X509Certificate2 CreateSelfSigned(string commonName)
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(ServerAuthOid)], false));

        SubjectAlternativeNameBuilder subjectAlternativeName = new();
        subjectAlternativeName.AddDnsName(commonName);
        request.CertificateExtensions.Add(subjectAlternativeName.Build());

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }
}
