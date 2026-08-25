namespace DockYarp.Tls;

using System;
using System.IO.Abstractions;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

/// <summary>Loads a PEM certificate/key pair into a certificate whose private key is usable for TLS.</summary>
internal static class PemCertificateLoader
{
    // The RFC 7468 label .NET writes/expects for a password-encrypted PKCS8 key — checked directly against the
    // PEM text so plain-vs-encrypted is decided from the file itself, never from whether encryption happens to
    // be configured (an operator-provided plain key must keep loading even once encryption is turned on).
    private const string EncryptedLabel = "ENCRYPTED PRIVATE KEY";

    /// <summary>Tries to load a certificate from a PEM certificate and key file.</summary>
    /// <param name="fileSystem">The file system abstraction.</param>
    /// <param name="certPath">Path to the PEM certificate file — a single certificate or a full chain (leaf
    /// plus one or more intermediates, in any order).</param>
    /// <param name="keyPath">Path to the PEM private-key file (RSA or EC), plain or encrypted.</param>
    /// <param name="passphrases">The current and (optional) previous key-encryption passphrase.</param>
    /// <param name="result">The loaded certificate, with its full chain preserved, plus whether it still needs
    /// re-encryption onto the current passphrase, when both files exist and the key matches one of the
    /// certificates.</param>
    /// <returns><see langword="true"/> when both files exist, the key matches a certificate in
    /// <paramref name="certPath"/>, and the certificate was loaded.</returns>
    /// <exception cref="InvalidOperationException">The key PEM is encrypted and neither passphrase in
    /// <paramref name="passphrases"/> decrypts it.</exception>
    public static bool TryLoad(
        IFileSystem fileSystem,
        string certPath,
        string keyPath,
        PrivateKeyPassphrases passphrases,
        out PemLoadResult result)
    {
        result = null!;
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
            string keyPem = fileSystem.File.ReadAllText(keyPath);
            (AsymmetricAlgorithm? key, bool usedCurrentPassphrase) = ImportPrivateKey(keyPem, keyPath, passphrases);
            using (key)
            {
                if (key is null || !TryBuildKeyedChain(parsed, key, out LoadedCertificate certificate))
                {
                    return false;
                }

                result = new PemLoadResult
                {
                    Certificate = certificate,
                    RequiresReencryption = passphrases.Current is { Length: > 0 } && !usedCurrentPassphrase,
                };
                return true;
            }
        }
        finally
        {
            foreach (X509Certificate2 entry in parsed)
            {
                entry.Dispose();
            }
        }
    }

    // Decrypts/imports the key exactly once, regardless of how many candidate certificates it is tried
    // against — for an encrypted key this avoids re-running the deliberately-slow PBE KDF per candidate, and
    // lets a decryption failure surface as its own loud, actionable exception instead of being absorbed into
    // the per-candidate "doesn't match, try next" flow below, which is correct for public-key mismatches but
    // wrong for a bad passphrase. The bool return also reports whether the *current* passphrase specifically
    // decrypted the key (as opposed to a plain key, or a fallback to the previous passphrase) so the caller can
    // flag hosts that still need the dashboard's re-encryption action.
    private static (AsymmetricAlgorithm? Key, bool UsedCurrentPassphrase) ImportPrivateKey(
        string keyPem, string keyPath, PrivateKeyPassphrases passphrases)
    {
        if (!keyPem.Contains(EncryptedLabel, StringComparison.Ordinal))
        {
            return (ImportPlain(keyPem), false);
        }

        if (passphrases.Current is { Length: > 0 } && TryImportEncrypted(keyPem, passphrases.Current, out AsymmetricAlgorithm? key))
        {
            return (key, true);
        }

        if (passphrases.Previous is { Length: > 0 } && TryImportEncrypted(keyPem, passphrases.Previous, out key))
        {
            return (key, false);
        }

        throw new InvalidOperationException(
            $"The encrypted private key '{keyPath}' could not be decrypted with the configured " +
            "Tls:PrivateKeyEncryptionPassphrase or Tls:PreviousPrivateKeyEncryptionPassphrase.");
    }

    // Tries RSA, then EC — the two private-key algorithms this loader has ever documented supporting. A
    // malformed/unrecognized plain key returns null (skipped by the caller), matching this method's
    // longstanding behavior — unlike an encrypted key's decryption failure above, this is not a new fail-fast
    // case introduced by this change.
    private static AsymmetricAlgorithm? ImportPlain(string keyPem)
    {
        RSA rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(keyPem);
            return rsa;
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            rsa.Dispose();
        }

        ECDsa ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportFromPem(keyPem);
            return ecdsa;
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            ecdsa.Dispose();
            return null;
        }
    }

    private static bool TryImportEncrypted(string keyPem, string password, out AsymmetricAlgorithm? key)
    {
        RSA rsa = RSA.Create();
        try
        {
            rsa.ImportFromEncryptedPem(keyPem, password);
            key = rsa;
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            rsa.Dispose();
        }

        ECDsa ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportFromEncryptedPem(keyPem, password);
            key = ecdsa;
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            ecdsa.Dispose();
            key = null;
            return false;
        }
    }

    // The private key does not always belong to the first certificate in the file — most real ACME tooling
    // writes the leaf first, but this does not assume that; it tries every certificate in the chain and keeps
    // whichever one the key actually matches.
    private static bool TryBuildKeyedChain(X509Certificate2Collection parsed, AsymmetricAlgorithm key, out LoadedCertificate certificate)
    {
        certificate = null!;
        for (int index = 0; index < parsed.Count; index++)
        {
            X509Certificate2? keyed = TryAttachPrivateKey(parsed[index], key);
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

                // Round-trip through PKCS12 so the private key is usable for TLS across platforms, and reload
                // as a collection (not a single certificate) so the additional/intermediate certificates
                // travel with the leaf instead of being silently dropped — the same shape an ACME-issued
                // certificate uses (see AcmeClient).
                // Never null: exporting a non-empty X509Certificate2Collection as PKCS12 always produces bytes.
                byte[] pkcs12 = full.Export(X509ContentType.Pkcs12)!;
                certificate = CertificateCollectionLoader.LoadKeyed(pkcs12, null);
                return true;
            }
        }

        return false;
    }

    // ArgumentException here means the key's public half doesn't match this candidate certificate — try the
    // next one; decryption/parsing failures were already resolved (or thrown) once, up in ImportPrivateKey.
    private static X509Certificate2? TryAttachPrivateKey(X509Certificate2 candidate, AsymmetricAlgorithm key)
    {
        try
        {
            return key switch
            {
                RSA rsa => candidate.CopyWithPrivateKey(rsa),
                ECDsa ecdsa => candidate.CopyWithPrivateKey(ecdsa),
                _ => null,
            };
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
