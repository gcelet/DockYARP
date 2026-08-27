namespace DockYarp.AdminApi;

using System.Threading;
using System.Threading.Tasks;

/// <summary>Lets the admin dashboard revoke a stored certificate via ACME.</summary>
/// <remarks>Implemented by the host over the TLS certificate store and ACME client, keeping AdminApi decoupled
/// from TLS — the same pattern as <see cref="ICertificateConverter"/>. Kept as its own interface rather than
/// added to <see cref="ICertificateConverter"/>: revocation is a materially higher-consequence action (it
/// takes the host offline until re-provisioning completes, and makes an irreversible call to the CA) than that
/// interface's existing format-only rewrites, and reads oddly bundled under a "converter" name.</remarks>
public interface ICertificateRevoker
{
    /// <summary>Revokes the stored certificate for a host via ACME, then removes it from the certificate
    /// store so the provisioning/renewal reconcile loop requests a fresh one (with a fresh key) on its next
    /// pass.</summary>
    /// <param name="host">The host whose certificate to revoke.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><see langword="true"/> when a certificate was found for <paramref name="host"/> and revoked;
    /// <see langword="false"/> when no certificate is stored for it.</returns>
    Task<bool> RevokeCertificateAsync(string host, CancellationToken cancellationToken);
}
