namespace DockYarp.Core.Models;

/// <summary>The outcome of verifying a connection's client certificate against the configured CA and CRL.</summary>
public enum ClientCertificateVerificationStatus
{
    /// <summary>No client certificate was presented on the connection.</summary>
    NotPresented,

    /// <summary>A client certificate was presented and chains to the configured CA without being revoked.</summary>
    Verified,

    /// <summary>A client certificate was presented but does not chain to the configured CA, or is revoked.</summary>
    Failed,
}
