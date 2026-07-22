namespace DockYarp.Security;

/// <summary>Reports whether a usable certificate is available for a host (used to gate HTTPS redirection).</summary>
/// <remarks>Implemented outside the security module (over the certificate store) to avoid a dependency on TLS.</remarks>
public interface ICertificateAvailability
{
    /// <summary>Reports whether a certificate is available to serve HTTPS for the given host.</summary>
    /// <param name="host">The request host.</param>
    /// <returns><see langword="true"/> when a certificate (exact or wildcard parent) exists for the host.</returns>
    bool IsAvailable(string host);
}
