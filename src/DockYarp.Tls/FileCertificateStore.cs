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
    private readonly Dictionary<string, X509Certificate2> certificates = new(StringComparer.OrdinalIgnoreCase);
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
    public X509Certificate2? Find(string host)
    {
        lock (gate)
        {
            return certificates.TryGetValue(host, out X509Certificate2? certificate) ? certificate : null;
        }
    }

    /// <inheritdoc />
    public void Save(string host, X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        fileSystem.Directory.CreateDirectory(directory);
        fileSystem.File.WriteAllBytes(PathFor(host, ".pfx"), certificate.Export(X509ContentType.Pfx));

        lock (gate)
        {
            if (certificates.Remove(host, out X509Certificate2? previous))
            {
                previous.Dispose();
            }

            certificates[host] = certificate;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<CertificateInfo> List()
    {
        lock (gate)
        {
            return [.. certificates.Select(entry => new CertificateInfo(entry.Key, new DateTimeOffset(entry.Value.NotAfter)))];
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (gate)
        {
            foreach (X509Certificate2 certificate in certificates.Values)
            {
                certificate.Dispose();
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

        // Load PFX first, then PEM, so a mounted PEM certificate overrides an ACME-persisted PFX for the same host.
        foreach (string file in fileSystem.Directory.EnumerateFiles(directory, "*.pfx"))
        {
            string host = fileSystem.Path.GetFileNameWithoutExtension(file);
            certificates[host] = X509CertificateLoader.LoadPkcs12(fileSystem.File.ReadAllBytes(file), null);
        }

        foreach (string file in fileSystem.Directory.EnumerateFiles(directory, "*.crt"))
        {
            if (TryLoadPem(file, out X509Certificate2? certificate))
            {
                string host = fileSystem.Path.GetFileNameWithoutExtension(file);
                certificates[host] = certificate;
            }
        }
    }

    private bool TryLoadPem(string certPath, out X509Certificate2 certificate)
    {
        certificate = null!;
        string keyPath = fileSystem.Path.ChangeExtension(certPath, ".key");
        if (!fileSystem.File.Exists(keyPath))
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

    private string PathFor(string host, string extension) => fileSystem.Path.Combine(directory, $"{host}{extension}");
}
