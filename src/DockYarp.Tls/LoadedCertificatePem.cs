namespace DockYarp.Tls;

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

/// <summary>PEM serialization for a <see cref="LoadedCertificate"/>.</summary>
public static class LoadedCertificatePem
{
    /// <summary>Exports the leaf and any additional (chain) certificates as concatenated PEM text.</summary>
    /// <param name="certificate">The certificate to export.</param>
    /// <returns>The full-chain PEM text (leaf first, then each additional certificate).</returns>
    public static string ExportChainPem(this LoadedCertificate certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        return string.Join(
            '\n', new[] { certificate.Leaf }.Concat(certificate.Additional).Select(c => c.ExportCertificatePem()));
    }

    /// <summary>Exports the leaf's private key as PEM text.</summary>
    /// <param name="certificate">The certificate whose leaf's private key is exported.</param>
    /// <returns>The private key, PKCS8-encoded PEM text.</returns>
    /// <exception cref="InvalidOperationException">The leaf has no exportable RSA or EC private key.</exception>
    // Tries RSA, then EC — the two private-key algorithms this project has ever documented supporting (see
    // PemCertificateLoader.TryAttachPrivateKey, which does the equivalent try-order on import).
    public static string ExportPrivateKeyPem(this LoadedCertificate certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        X509Certificate2 leaf = certificate.Leaf;

        using RSA? rsa = leaf.GetRSAPrivateKey();
        if (rsa is not null)
        {
            return rsa.ExportPkcs8PrivateKeyPem();
        }

        using ECDsa? ecdsa = leaf.GetECDsaPrivateKey();
        if (ecdsa is not null)
        {
            return ecdsa.ExportPkcs8PrivateKeyPem();
        }

        throw new InvalidOperationException($"The certificate for '{leaf.Subject}' has no exportable RSA or EC private key.");
    }
}
