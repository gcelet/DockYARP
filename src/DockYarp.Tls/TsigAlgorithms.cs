namespace DockYarp.Tls;

/// <summary>Parses and formats TSIG algorithm names.</summary>
internal static class TsigAlgorithms
{
    /// <summary>Parses a configured algorithm name (case-insensitive) into a <see cref="TsigAlgorithm"/>.</summary>
    /// <param name="value">The algorithm name (e.g. <c>hmac-sha256</c>).</param>
    /// <returns>The parsed algorithm.</returns>
    /// <exception cref="System.NotSupportedException">The name is not a recognized TSIG algorithm.</exception>
    public static TsigAlgorithm Parse(string value) => value.ToLowerInvariant() switch
    {
        "hmac-sha1" => TsigAlgorithm.HmacSha1,
        "hmac-sha256" => TsigAlgorithm.HmacSha256,
        "hmac-sha384" => TsigAlgorithm.HmacSha384,
        "hmac-sha512" => TsigAlgorithm.HmacSha512,
        _ => throw new System.NotSupportedException($"Unsupported TSIG algorithm: '{value}'."),
    };

    /// <summary>Returns the DNS wire-format algorithm name (RFC 8945 §6), used as the TSIG RDATA algorithm name.</summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <returns>The dotted wire name, e.g. <c>hmac-sha256.</c>.</returns>
    public static string ToWireName(TsigAlgorithm algorithm) => algorithm switch
    {
        TsigAlgorithm.HmacSha1 => "hmac-sha1.",
        TsigAlgorithm.HmacSha256 => "hmac-sha256.",
        TsigAlgorithm.HmacSha384 => "hmac-sha384.",
        TsigAlgorithm.HmacSha512 => "hmac-sha512.",
        _ => throw new System.NotSupportedException($"Unsupported TSIG algorithm: {algorithm}."),
    };
}
