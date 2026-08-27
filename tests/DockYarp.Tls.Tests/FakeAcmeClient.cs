namespace DockYarp.Tls.Tests;

using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using DockYarp.Core.Models;
using DockYarp.Tls;

/// <summary>A fake <see cref="IAcmeClient"/> that returns a self-signed certificate and counts requests.</summary>
internal sealed class FakeAcmeClient : IAcmeClient
{
    private readonly ConcurrentDictionary<string, int> requests = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> revocations = new(System.StringComparer.OrdinalIgnoreCase);

    public int RequestCount(string host) => requests.TryGetValue(host, out int count) ? count : 0;

    public int RevocationCount(string host) => revocations.TryGetValue(host, out int count) ? count : 0;

    public Task<LoadedCertificate> RequestCertificateAsync(
        string host, string? email, AcmeChallengeType challengeType, CancellationToken cancellationToken)
    {
        requests.AddOrUpdate(host, 1, static (_, current) => current + 1);
        return Task.FromResult(new LoadedCertificate(DefaultCertificateFactory.CreateSelfSigned(host), []));
    }

    public Task RevokeCertificateAsync(
        string host, string? email, X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        revocations.AddOrUpdate(host, 1, static (_, current) => current + 1);
        return Task.CompletedTask;
    }
}
