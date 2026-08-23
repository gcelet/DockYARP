namespace DockYarp.Tls;

using System;
using System.Security.Cryptography;

/// <summary>A TSIG key (RFC 8945) used to authenticate a DNS UPDATE message.</summary>
/// <param name="Name">The key name, as configured on the DNS server.</param>
/// <param name="Secret">The shared secret, base64-decoded.</param>
/// <param name="Algorithm">The HMAC algorithm identifying both the .NET implementation and the wire name.</param>
internal sealed record TsigKey(string Name, byte[] Secret, TsigAlgorithm Algorithm)
{
    /// <summary>Decodes a base64-encoded secret and validates the algorithm name.</summary>
    /// <param name="name">The key name.</param>
    /// <param name="base64Secret">The shared secret, base64-encoded.</param>
    /// <param name="algorithmName">The algorithm name (case-insensitive; e.g. <c>hmac-sha256</c>).</param>
    /// <returns>The parsed key.</returns>
    public static TsigKey Parse(string name, string base64Secret, string algorithmName) =>
        new(name, Convert.FromBase64String(base64Secret), TsigAlgorithms.Parse(algorithmName));

    /// <summary>Computes the HMAC over <paramref name="data"/> using this key's secret and algorithm.</summary>
    /// <param name="data">The canonical bytes to sign (RFC 8945 §4.3.1).</param>
    /// <returns>The MAC bytes.</returns>
    public byte[] ComputeMac(ReadOnlySpan<byte> data) => Algorithm switch
    {
        TsigAlgorithm.HmacSha1 => HMACSHA1.HashData(Secret, data),
        TsigAlgorithm.HmacSha256 => HMACSHA256.HashData(Secret, data),
        TsigAlgorithm.HmacSha384 => HMACSHA384.HashData(Secret, data),
        TsigAlgorithm.HmacSha512 => HMACSHA512.HashData(Secret, data),
        _ => throw new NotSupportedException($"Unsupported TSIG algorithm: {Algorithm}."),
    };
}
