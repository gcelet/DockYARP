namespace DockYarp.Tls.Tests;

using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using DockYarp.Tls;

/// <summary>A fake <see cref="IAcmeClient"/> that returns a self-signed certificate and counts requests.</summary>
internal sealed class FakeAcmeClient : IAcmeClient
{
    private readonly ConcurrentDictionary<string, int> requests = new(System.StringComparer.OrdinalIgnoreCase);

    public int RequestCount(string host) => requests.TryGetValue(host, out int count) ? count : 0;

    public Task<X509Certificate2> RequestCertificateAsync(string host, string? email, CancellationToken cancellationToken)
    {
        requests.AddOrUpdate(host, 1, static (_, current) => current + 1);
        return Task.FromResult(DefaultCertificateFactory.CreateSelfSigned(host));
    }
}
