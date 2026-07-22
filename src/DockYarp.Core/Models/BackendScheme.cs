namespace DockYarp.Core.Models;

/// <summary>Transport scheme used to reach a cluster endpoint's backend.</summary>
public enum BackendScheme
{
    /// <summary>Plain HTTP.</summary>
    Http,

    /// <summary>HTTP over TLS.</summary>
    Https,
}
