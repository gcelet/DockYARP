namespace DockYarp.Tls;

/// <summary>Minimum TLS protocol version accepted on the HTTPS endpoint.</summary>
public enum TlsVersion
{
    /// <summary>TLS 1.2 (also enables TLS 1.3).</summary>
    Tls12,

    /// <summary>TLS 1.3 only.</summary>
    Tls13,
}
