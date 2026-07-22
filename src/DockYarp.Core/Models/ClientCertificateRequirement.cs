namespace DockYarp.Core.Models;

/// <summary>Whether a route requires a client certificate (mutual TLS).</summary>
public enum ClientCertificateRequirement
{
    /// <summary>No client certificate is requested or required.</summary>
    None,

    /// <summary>A client certificate is accepted if presented but not required.</summary>
    Optional,

    /// <summary>A valid client certificate is required; requests without one are rejected.</summary>
    Required,
}
