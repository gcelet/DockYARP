namespace DockYarp.Tls;

using System.IO.Abstractions;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

/// <summary>Loads a PEM certificate/key pair into a certificate whose private key is usable for TLS.</summary>
internal static class PemCertificateLoader
{
    /// <summary>Tries to load a certificate from a PEM certificate and key file.</summary>
    /// <param name="fileSystem">The file system abstraction.</param>
    /// <param name="certPath">Path to the PEM certificate file — a single certificate or a full chain (leaf
    /// plus one or more intermediates, in any order).</param>
    /// <param name="keyPath">Path to the PEM private-key file (RSA or EC).</param>
    /// <param name="certificate">The loaded certificate, with its full chain preserved, when both files
    /// exist and the key matches one of the certificates.</param>
    /// <returns><see langword="true"/> when both files exist, the key matches a certificate in
    /// <paramref name="certPath"/>, and the certificate was loaded.</returns>
    public static bool TryLoad(IFileSystem fileSystem, string certPath, string keyPath, out X509Certificate2 certificate)
    {
        certificate = null!;
        if (!fileSystem.File.Exists(certPath) || !fileSystem.File.Exists(keyPath))
        {
            return false;
        }

        X509Certificate2Collection parsed = new();
        parsed.ImportFromPem(fileSystem.File.ReadAllText(certPath));
        if (parsed.Count == 0)
        {
            return false;
        }

        try
        {
            return TryBuildKeyedChain(parsed, fileSystem.File.ReadAllText(keyPath), out certificate);
        }
        finally
        {
            foreach (X509Certificate2 entry in parsed)
            {
                entry.Dispose();
            }
        }
    }

    // The private key does not always belong to the first certificate in the file — most real ACME tooling
    // writes the leaf first, but this does not assume that; it tries every certificate in the chain and keeps
    // whichever one the key actually matches.
    private static bool TryBuildKeyedChain(X509Certificate2Collection parsed, string keyPem, out X509Certificate2 certificate)
    {
        certificate = null!;
        for (int index = 0; index < parsed.Count; index++)
        {
            X509Certificate2? keyed = TryAttachPrivateKey(parsed[index], keyPem);
            if (keyed is null)
            {
                continue;
            }

            using (keyed)
            {
                X509Certificate2Collection full = [];
                for (int i = 0; i < parsed.Count; i++)
                {
                    full.Add(i == index ? keyed : parsed[i]);
                }

                // Round-trip through PKCS12 so the private key is usable for TLS across platforms, and so the
                // full chain (not just the leaf) travels with the resulting certificate — mirroring how an
                // ACME-issued certificate is already persisted (see CertesAcmeClient.BuildPfx).
                // Never null: exporting a non-empty X509Certificate2Collection as PKCS12 always produces bytes.
                byte[] pkcs12 = full.Export(X509ContentType.Pkcs12)!;
                certificate = X509CertificateLoader.LoadPkcs12(pkcs12, null);
                return true;
            }
        }

        return false;
    }

    // Tries RSA, then EC — the two private-key algorithms this loader has ever documented supporting.
    // A CryptographicException means the PEM key text isn't this algorithm; an ArgumentException from
    // CopyWithPrivateKey means it is this algorithm but doesn't match this particular certificate's public
    // key — both mean "try the next certificate", not a real failure yet.
    private static X509Certificate2? TryAttachPrivateKey(X509Certificate2 candidate, string keyPem)
    {
        try
        {
            using RSA rsa = RSA.Create();
            rsa.ImportFromPem(keyPem);
            return candidate.CopyWithPrivateKey(rsa);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            // Not an RSA key, or it doesn't match this candidate's public key — fall through to try EC.
        }

        try
        {
            using ECDsa ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(keyPem);
            return candidate.CopyWithPrivateKey(ecdsa);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            return null;
        }
    }
}
