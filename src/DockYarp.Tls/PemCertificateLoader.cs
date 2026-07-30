namespace DockYarp.Tls;

using System.IO.Abstractions;
using System.Security.Cryptography.X509Certificates;

/// <summary>Loads a PEM certificate/key pair into a certificate whose private key is usable for TLS.</summary>
internal static class PemCertificateLoader
{
    /// <summary>Tries to load a certificate from a PEM certificate and key file.</summary>
    /// <param name="fileSystem">The file system abstraction.</param>
    /// <param name="certPath">Path to the PEM certificate file.</param>
    /// <param name="keyPath">Path to the PEM private-key file.</param>
    /// <param name="certificate">The loaded certificate when both files exist.</param>
    /// <returns><see langword="true"/> when both files exist and the certificate was loaded.</returns>
    public static bool TryLoad(IFileSystem fileSystem, string certPath, string keyPath, out X509Certificate2 certificate)
    {
        certificate = null!;
        if (!fileSystem.File.Exists(certPath) || !fileSystem.File.Exists(keyPath))
        {
            return false;
        }

        using X509Certificate2 pem = X509Certificate2.CreateFromPem(
            fileSystem.File.ReadAllText(certPath),
            fileSystem.File.ReadAllText(keyPath));

        // Round-trip through PFX so the private key is usable for TLS across platforms (avoids ephemeral-key issues).
        certificate = X509CertificateLoader.LoadPkcs12(pem.Export(X509ContentType.Pfx), null);
        return true;
    }
}
