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
    private readonly IFileSystem fileSystem;
    private readonly string directory;

    /// <summary>Creates the store and loads any existing certificates from the configured directory.</summary>
    /// <param name="options">TLS options carrying the certificate directory.</param>
    /// <param name="fileSystem">Filesystem abstraction used to read and write certificate files.</param>
    public FileCertificateStore(TlsOptions options, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        directory = options.CertificateDirectory;
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
        fileSystem.File.WriteAllText(PathFor(host, ".key"), certificate.ExportPrivateKeyPem());

        lock (gate)
        {
            if (certificates.Remove(host, out LoadedCertificate? previous))
            {
                DisposeCertificate(previous);
            }

            certificates[host] = certificate;
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
            if (PemCertificateLoader.TryLoad(fileSystem, file, keyPath, out LoadedCertificate certificate))
            {
                certificates[host] = certificate;
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
