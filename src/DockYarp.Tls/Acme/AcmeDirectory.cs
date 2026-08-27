namespace DockYarp.Tls.Acme;

using System.Text.Json.Serialization;

/// <summary>The ACME server's directory (RFC 8555 §7.1.1) — the entry point resource URLs.</summary>
internal sealed record AcmeDirectory
{
    /// <summary>URL to fetch a fresh replay-nonce from.</summary>
    [JsonPropertyName("newNonce")]
    public required string NewNonce { get; init; }

    /// <summary>URL to create a new account.</summary>
    [JsonPropertyName("newAccount")]
    public required string NewAccount { get; init; }

    /// <summary>URL to create a new order.</summary>
    [JsonPropertyName("newOrder")]
    public required string NewOrder { get; init; }

    /// <summary>URL to revoke a certificate (RFC 8555 §7.6).</summary>
    /// <remarks>Optional in the directory object per RFC 8555 §7.1.1 — unlike <see cref="NewNonce"/>/
    /// <see cref="NewAccount"/>/<see cref="NewOrder"/>, a CA is not required to support revocation.</remarks>
    [JsonPropertyName("revokeCert")]
    public string? RevokeCert { get; init; }
}
