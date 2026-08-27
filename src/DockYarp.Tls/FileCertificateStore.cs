namespace DockYarp.Tls;

using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

/// <summary>File-based certificate store: PFX and PEM files in a directory, mirrored in memory for fast SNI lookup.</summary>
public sealed class FileCertificateStore : ICertificateStore, IDisposable
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, LoadedCertificate> certificates = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> hostsRequiringReencryption = new(StringComparer.OrdinalIgnoreCase);
    private readonly IFileSystem fileSystem;
    private readonly string directory;
    private readonly string? encryptionPassphrase;
    private readonly string? previousEncryptionPassphrase;

    /// <summary>Creates the store and loads any existing certificates from the configured directory.</summary>
    /// <param name="options">TLS options carrying the certificate directory and optional key-encryption passphrases.</param>
    /// <param name="fileSystem">Filesystem abstraction used to read and write certificate files.</param>
    public FileCertificateStore(TlsOptions options, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        directory = options.CertificateDirectory;
        encryptionPassphrase = options.PrivateKeyEncryptionPassphrase;
        previousEncryptionPassphrase = options.PreviousPrivateKeyEncryptionPassphrase;
        Load();
    }

    /// <inheritdoc />
    public LoadedCertificate? Find(string host)
    {
        lock (gate)
        {
            return certificates.TryGetValue(host, out LoadedCertificate? certificate) ? certificate : null;
        }
    }

    /// <inheritdoc />
    public void Save(string host, LoadedCertificate certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        fileSystem.Directory.CreateDirectory(directory);
        fileSystem.File.WriteAllText(PathFor(host, ".crt"), certificate.ExportChainPem());
        fileSystem.File.WriteAllText(PathFor(host, ".key"), certificate.ExportPrivateKeyPem(encryptionPassphrase));

        lock (gate)
        {
            if (certificates.Remove(host, out LoadedCertificate? previous))
            {
                DisposeCertificate(previous);
            }

            certificates[host] = certificate;
            hostsRequiringReencryption.Remove(host);
        }
    }

    /// <inheritdoc />
    public bool IsPfxBacked(string host) =>
        fileSystem.File.Exists(PathFor(host, ".pfx")) && !fileSystem.File.Exists(PathFor(host, ".crt"));

    /// <inheritdoc />
    public bool RequiresKeyReencryption(string host)
    {
        lock (gate)
        {
            return hostsRequiringReencryption.Contains(host);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Writes the already-loaded certificate's PEM directly and does not call <see cref="Save"/>: <c>Save</c>'s
    /// remove-then-dispose logic assumes a genuinely new certificate is replacing the old one, but here the
    /// "new" and "old" are the same object — going through it would dispose the certificate this method is
    /// meant to leave usable, breaking the next SNI lookup for this host.
    /// </remarks>
    public bool ConvertToPem(string host)
    {
        lock (gate)
        {
            if (!certificates.TryGetValue(host, out LoadedCertificate? certificate))
            {
                return false;
            }

            fileSystem.File.WriteAllText(PathFor(host, ".crt"), certificate.ExportChainPem());
            fileSystem.File.WriteAllText(PathFor(host, ".key"), certificate.ExportPrivateKeyPem(encryptionPassphrase));
            hostsRequiringReencryption.Remove(host);

            string pfxPath = PathFor(host, ".pfx");
            if (fileSystem.File.Exists(pfxPath))
            {
                fileSystem.File.Delete(pfxPath);
            }

            return true;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Same "write the already-loaded certificate directly, never call <see cref="Save"/>" rationale as
    /// <see cref="ConvertToPem"/> — the in-memory object is unchanged, only its on-disk key encryption. Unlike
    /// <see cref="ConvertToPem"/>, this never touches <c>.crt</c> content or a <c>.pfx</c> file: it exists
    /// specifically to rewrite <c>.key</c> under the currently configured passphrase, whether that's a
    /// first-time enable (the key was plain) or a rotation (the key was encrypted with a previous passphrase).
    /// </remarks>
    public bool ReencryptPrivateKey(string host)
    {
        lock (gate)
        {
            if (!certificates.TryGetValue(host, out LoadedCertificate? certificate))
            {
                return false;
            }

            fileSystem.File.WriteAllText(PathFor(host, ".key"), certificate.ExportPrivateKeyPem(encryptionPassphrase));
            hostsRequiringReencryption.Remove(host);
            return true;
        }
    }

    /// <inheritdoc />
    public bool Remove(string host)
    {
        lock (gate)
        {
            if (!certificates.Remove(host, out LoadedCertificate? certificate))
            {
                return false;
            }

            DisposeCertificate(certificate);
            hostsRequiringReencryption.Remove(host);

            DeleteIfExists(PathFor(host, ".crt"));
            DeleteIfExists(PathFor(host, ".key"));
            DeleteIfExists(PathFor(host, ".pfx"));
            return true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<CertificateInfo> List()
    {
        lock (gate)
        {
            return [.. certificates.Select(entry => new CertificateInfo(entry.Key, new DateTimeOffset(entry.Value.Leaf.NotAfter)))];
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (gate)
        {
            foreach (LoadedCertificate certificate in certificates.Values)
            {
                DisposeCertificate(certificate);
            }

            certificates.Clear();
            hostsRequiringReencryption.Clear();
        }
    }

    private void Load()
    {
        if (!fileSystem.Directory.Exists(directory))
        {
            return;
        }

        // PFX is loaded into a separate working set first, then PEM is loaded directly into `certificates` —
        // so a PEM pair can never be overwritten by a PFX read, regardless of directory enumeration order.
        // Only PFX entries whose host has no PEM pair are merged in afterward: a legacy PFX left over from
        // before this store started writing PEM (or an operator-provided one) never wins over a PEM pair for
        // the same host.
        Dictionary<string, LoadedCertificate> pfxOnly = new(StringComparer.OrdinalIgnoreCase);
        foreach (string file in fileSystem.Directory.EnumerateFiles(directory, "*.pfx"))
        {
            string host = fileSystem.Path.GetFileNameWithoutExtension(file);
            if (IsReserved(host))
            {
                continue;
            }

            pfxOnly[host] = CertificateCollectionLoader.LoadKeyed(fileSystem.File.ReadAllBytes(file), null);
        }

        foreach (string file in fileSystem.Directory.EnumerateFiles(directory, "*.crt"))
        {
            string host = fileSystem.Path.GetFileNameWithoutExtension(file);
            if (IsReserved(host))
            {
                continue;
            }

            string keyPath = fileSystem.Path.ChangeExtension(file, ".key");
            PrivateKeyPassphrases passphrases = new(encryptionPassphrase, previousEncryptionPassphrase);
            if (PemCertificateLoader.TryLoad(fileSystem, file, keyPath, passphrases, out PemLoadResult result))
            {
                certificates[host] = result.Certificate;
                if (result.RequiresReencryption)
                {
                    hostsRequiringReencryption.Add(host);
                }

                if (pfxOnly.Remove(host, out LoadedCertificate? shadowed))
                {
                    DisposeCertificate(shadowed);
                }
            }
        }

        foreach ((string host, LoadedCertificate certificate) in pfxOnly)
        {
            certificates[host] = certificate;
        }
    }

    private void DeleteIfExists(string path)
    {
        if (fileSystem.File.Exists(path))
        {
            fileSystem.File.Delete(path);
        }
    }

    private static void DisposeCertificate(LoadedCertificate certificate)
    {
        certificate.Leaf.Dispose();
        foreach (X509Certificate2 additional in certificate.Additional)
        {
            additional.Dispose();
        }
    }

    // The "default" basename is owned by the SNI fallback provider (default.crt/default.key), not a vhost.
    private static bool IsReserved(string host) => string.Equals(host, "default", StringComparison.OrdinalIgnoreCase);

    private string PathFor(string host, string extension) => fileSystem.Path.Combine(directory, $"{host}{extension}");
}
