namespace DockYarp.Tls;

using System.Threading;
using System.Threading.Tasks;

using DockYarp.Core.Models;

/// <summary>Obtains a certificate for a host from an ACME provider.</summary>
/// <remarks>The concrete implementation performs the network exchange with the CA; it is mocked in tests.</remarks>
public interface IAcmeClient
{
    /// <summary>Requests a certificate for the given host.</summary>
    /// <param name="host">The host to certify. A leading <c>*.</c> requests a wildcard certificate (DNS-01 only).</param>
    /// <param name="email">Contact email, or <see langword="null"/> to use the configured default.</param>
    /// <param name="challengeType">Which ACME challenge to use for this host.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The issued certificate, with any intermediate certificates it was issued alongside.</returns>
    Task<LoadedCertificate> RequestCertificateAsync(
        string host, string? email, AcmeChallengeType challengeType, CancellationToken cancellationToken);
}
