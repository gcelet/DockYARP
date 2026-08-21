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

    // AES-256-CBC + SHA-256 + 600,000 iterations: OWASP's current minimum recommendation for PBKDF2-HMAC-SHA256
    // (Password Storage Cheat Sheet). Not operator-configurable — tuning the KDF has no realistic operator
    // benefit here and would be complexity for its own sake.
    private static readonly PbeParameters EncryptionParameters =
        new(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, iterationCount: 600_000);

    /// <summary>Exports the leaf's private key as PEM text, optionally encrypted.</summary>
    /// <param name="certificate">The certificate whose leaf's private key is exported.</param>
    /// <param name="passphrase">When non-empty, the private key is encrypted (PKCS8
    /// <c>ENCRYPTED PRIVATE KEY</c>) under this passphrase; when null/empty, the key is exported as plain
    /// PKCS8 PEM (today's default behavior).</param>
    /// <returns>The private key, PKCS8-encoded PEM text (plain or encrypted).</returns>
    /// <exception cref="InvalidOperationException">The leaf has no exportable RSA or EC private key.</exception>
    // Tries RSA, then EC — the two private-key algorithms this project has ever documented supporting (see
    // PemCertificateLoader, which does the equivalent try-order on import).
    public static string ExportPrivateKeyPem(this LoadedCertificate certificate, string? passphrase)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        X509Certificate2 leaf = certificate.Leaf;

        using RSA? rsa = leaf.GetRSAPrivateKey();
        if (rsa is not null)
        {
            return passphrase is { Length: > 0 }
                ? rsa.ExportEncryptedPkcs8PrivateKeyPem(passphrase, EncryptionParameters)
                : rsa.ExportPkcs8PrivateKeyPem();
        }

        using ECDsa? ecdsa = leaf.GetECDsaPrivateKey();
        if (ecdsa is not null)
        {
            return passphrase is { Length: > 0 }
                ? ecdsa.ExportEncryptedPkcs8PrivateKeyPem(passphrase, EncryptionParameters)
                : ecdsa.ExportPkcs8PrivateKeyPem();
        }

        throw new InvalidOperationException($"The certificate for '{leaf.Subject}' has no exportable RSA or EC private key.");
    }
}
