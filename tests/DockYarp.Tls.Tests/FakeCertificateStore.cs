namespace DockYarp.Tls.Tests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

using DockYarp.Tls;

/// <summary>A thread-safe in-memory <see cref="ICertificateStore"/> for provisioning tests.</summary>
internal sealed class FakeCertificateStore : ICertificateStore
{
    private readonly ConcurrentDictionary<string, X509Certificate2> certificates = new(StringComparer.OrdinalIgnoreCase);

    public X509Certificate2? Find(string host) =>
        certificates.TryGetValue(host, out X509Certificate2? certificate) ? certificate : null;

    public void Save(string host, X509Certificate2 certificate) => certificates[host] = certificate;

    public IReadOnlyList<CertificateInfo> List() =>
        [.. certificates.Select(entry => new CertificateInfo(entry.Key, new DateTimeOffset(entry.Value.NotAfter)))];
}
