namespace DockYarp.AdminApi;

using System.Collections.Generic;

/// <summary>Provides the admin API with a sanitized view of stored certificates.</summary>
/// <remarks>Implemented by the host over the TLS certificate store, keeping AdminApi decoupled from TLS.</remarks>
public interface ICertificateInventory
{
    /// <summary>Lists the stored certificates (host and expiry, no private keys).</summary>
    /// <returns>The certificate views.</returns>
    IReadOnlyList<AdminApiModels.CertView> List();
}
