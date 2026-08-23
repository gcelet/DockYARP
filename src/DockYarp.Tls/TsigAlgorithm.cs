namespace DockYarp.Tls;

/// <summary>A TSIG HMAC algorithm (RFC 8945 §6 registered algorithm names).</summary>
internal enum TsigAlgorithm
{
    /// <summary><c>hmac-sha1</c>.</summary>
    HmacSha1,

    /// <summary><c>hmac-sha256</c> (the default).</summary>
    HmacSha256,

    /// <summary><c>hmac-sha384</c>.</summary>
    HmacSha384,

    /// <summary><c>hmac-sha512</c>.</summary>
    HmacSha512,
}
