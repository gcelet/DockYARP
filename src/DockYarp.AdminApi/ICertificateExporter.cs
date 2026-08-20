namespace DockYarp.AdminApi;

/// <summary>Provides the admin dashboard with a stored certificate's exportable material, including its
/// private key.</summary>
/// <remarks>Implemented by the host over the TLS certificate store, keeping AdminApi decoupled from TLS. Kept
/// deliberately separate from <see cref="ICertificateInventory"/>, whose contract is explicitly "no private
/// keys" — this interface exists exactly to cross that boundary for the opt-in dashboard download feature
/// (<c>AdminApi:AllowCertificateDownload</c>).</remarks>
public interface ICertificateExporter
{
    /// <summary>Gets a stored certificate's exportable PEM material for a host.</summary>
    /// <param name="host">The host to look up.</param>
    /// <returns>The certificate's PEM material, or <see langword="null"/> when no certificate is stored for
    /// <paramref name="host"/>.</returns>
    CertificateExport? Export(string host);
}
