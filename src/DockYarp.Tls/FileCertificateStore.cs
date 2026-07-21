namespace DockYarp.Tls;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

/// <summary>File-based certificate store: PFX files in a directory, mirrored in memory for fast SNI lookup.</summary>
public sealed class FileCertificateStore : ICertificateStore, IDisposable
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, X509Certificate2> certificates = new(StringComparer.OrdinalIgnoreCase);
    private readonly string directory;

    /// <summary>Creates the store and loads any existing certificates from the configured directory.</summary>
    /// <param name="options">TLS options carrying the certificate directory.</param>
    public FileCertificateStore(TlsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
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
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(PathFor(host), certificate.Export(X509ContentType.Pfx));

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
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(directory, "*.pfx"))
        {
            string host = Path.GetFileNameWithoutExtension(file);
            certificates[host] = X509CertificateLoader.LoadPkcs12FromFile(file, null);
        }
    }

    private string PathFor(string host) => Path.Combine(directory, $"{host}.pfx");
}
