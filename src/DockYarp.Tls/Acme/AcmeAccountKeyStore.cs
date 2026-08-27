namespace DockYarp.Tls.Acme;

using System;
using System.IO.Abstractions;
using System.Security.Cryptography;

/// <summary>Persists and loads the ACME account key <see cref="AcmeClient"/> reuses across requests — RFC 8555
/// <c>newAccount</c> idempotency resolves repeated calls signed with the same key to the same account, so
/// reusing a persisted key avoids registering a throwaway account on every certificate request/renewal. Scoped
/// per (contact email, ACME directory endpoint) pair — mirroring a real acme-companion installation's own
/// on-disk layout — so hosts declaring distinct <c>LETSENCRYPT_EMAIL</c> values keep getting independent
/// accounts, exactly as they do today without persistence.</summary>
internal static class AcmeAccountKeyStore
{
    /// <summary>Loads the persisted EC P-256 account key for the given (contact email, ACME directory
    /// endpoint) pair, generating and persisting one first if none exists yet.</summary>
    /// <param name="fileSystem">Filesystem abstraction used to read/write the key file.</param>
    /// <param name="certificateDirectory"><see cref="TlsOptions.CertificateDirectory"/>.</param>
    /// <param name="acmeDirectoryUri">The ACME directory endpoint the account is scoped to.</param>
    /// <param name="contactEmail">The request's resolved contact email the account is scoped to.</param>
    /// <returns>A fresh <see cref="ECDsa"/> instance built from the persisted key material — never shared
    /// across calls, so concurrent callers each get their own instance signing with the same key content.</returns>
    /// <exception cref="InvalidOperationException">The persisted file exists but is not a readable EC P-256
    /// PEM key (for example an RSA key, or a corrupt file).</exception>
    public static ECDsa LoadOrCreate(
        IFileSystem fileSystem, string certificateDirectory, Uri acmeDirectoryUri, string contactEmail)
    {
        string path = ResolvePath(fileSystem, certificateDirectory, acmeDirectoryUri, contactEmail);
        if (fileSystem.File.Exists(path))
        {
            return Import(fileSystem.File.ReadAllText(path), path);
        }

        ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(path)!);
        fileSystem.File.WriteAllText(path, key.ExportPkcs8PrivateKeyPem());
        return key;
    }

    /// <summary>Resolves the (contact email, ACME directory endpoint)-scoped account key path — an operator
    /// migrating an existing EC-keyed account places its PEM file here before DockYarp's first request for
    /// that pair.</summary>
    public static string ResolvePath(
        IFileSystem fileSystem, string certificateDirectory, Uri acmeDirectoryUri, string contactEmail)
    {
        string[] directoryPathSegments = acmeDirectoryUri.AbsolutePath.Split(
            '/', StringSplitOptions.RemoveEmptyEntries);
        string[] parts =
        [
            certificateDirectory, "acme", contactEmail, acmeDirectoryUri.Host, .. directoryPathSegments,
            "account.key",
        ];
        return fileSystem.Path.Combine(parts);
    }

    private static ECDsa Import(string pem, string path)
    {
        try
        {
            ECDsa key = ECDsa.Create();
            key.ImportFromPem(pem);
            return key;
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            if (IsRsaKey(pem))
            {
                throw new InvalidOperationException(
                    $"The ACME account key at '{path}' is an RSA key. Only EC P-256 account keys can be "
                    + "imported; RSA account-key import is not supported.", ex);
            }

            throw new InvalidOperationException($"The ACME account key at '{path}' could not be read.", ex);
        }
    }

    private static bool IsRsaKey(string pem)
    {
        try
        {
            using RSA rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            return false;
        }
    }
}
