namespace DockYarp.Tls.Acme;

using System.Text.Json.Serialization;

/// <summary>An authorization resource (RFC 8555 §7.1.4).</summary>
internal sealed record AcmeAuthorization
{
    /// <summary>The authorization's lifecycle status.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>The challenges offered for this authorization (HTTP-01, DNS-01, ...).</summary>
    [JsonPropertyName("challenges")]
    public required AcmeChallenge[] Challenges { get; init; }
}
