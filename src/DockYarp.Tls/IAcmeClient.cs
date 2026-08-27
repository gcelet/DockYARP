namespace DockYarp.Tls;

using System.Security.Cryptography.X509Certificates;
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

    /// <summary>Revokes a certificate via the ACME provider (RFC 8555 §7.6).</summary>
    /// <param name="host">The host the certificate belongs to (resolves which persisted account key signs the
    /// revocation request).</param>
    /// <param name="email">Contact email, or <see langword="null"/> to use the configured default — must match
    /// what the certificate was originally requested with, so the same persisted account key is resolved.</param>
    /// <param name="certificate">The certificate to revoke.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <exception cref="System.InvalidOperationException">No persisted account key exists yet for the
    /// resolved (email, endpoint) pair, or the CA's directory does not support revocation.</exception>
    Task RevokeCertificateAsync(
        string host, string? email, X509Certificate2 certificate, CancellationToken cancellationToken);
}
