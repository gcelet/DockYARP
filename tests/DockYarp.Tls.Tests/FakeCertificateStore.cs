namespace DockYarp.Tls.Tests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using DockYarp.Tls;

/// <summary>A thread-safe in-memory <see cref="ICertificateStore"/> for provisioning tests.</summary>
internal sealed class FakeCertificateStore : ICertificateStore
{
    private readonly ConcurrentDictionary<string, LoadedCertificate> certificates = new(StringComparer.OrdinalIgnoreCase);

    public LoadedCertificate? Find(string host) =>
        certificates.TryGetValue(host, out LoadedCertificate? certificate) ? certificate : null;

    public void Save(string host, LoadedCertificate certificate) => certificates[host] = certificate;

    public IReadOnlyList<CertificateInfo> List() =>
        [.. certificates.Select(entry => new CertificateInfo(entry.Key, new DateTimeOffset(entry.Value.Leaf.NotAfter)))];
}
