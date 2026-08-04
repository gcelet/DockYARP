namespace DockYarp.Tls;

/// <summary>Outcome of parsing a PROXY protocol header from a connection buffer.</summary>
public enum ProxyProtocolParseStatus
{
    /// <summary>The buffer does not yet contain a complete header; read more bytes and retry.</summary>
    NeedMoreData,

    /// <summary>The buffer does not start with a valid PROXY protocol header.</summary>
    Invalid,

    /// <summary>A complete header was parsed.</summary>
    Done,
}
