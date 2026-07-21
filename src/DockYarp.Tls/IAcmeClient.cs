namespace DockYarp.Tls;

using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Obtains a certificate for a host from an ACME provider.</summary>
/// <remarks>The concrete implementation performs the network exchange with the CA; it is mocked in tests.</remarks>
public interface IAcmeClient
{
    /// <summary>Requests a certificate for the given host.</summary>
    /// <param name="host">The host to certify.</param>
    /// <param name="email">Contact email, or <see langword="null"/> to use the configured default.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The issued certificate.</returns>
    Task<X509Certificate2> RequestCertificateAsync(string host, string? email, CancellationToken cancellationToken);
}
