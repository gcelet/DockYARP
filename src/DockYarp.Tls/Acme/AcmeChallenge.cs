namespace DockYarp.Tls.Acme;

using System.Text.Json.Serialization;

/// <summary>A challenge resource (RFC 8555 §7.1.5, §8).</summary>
internal sealed record AcmeChallenge
{
    /// <summary>The challenge type — <c>"http-01"</c> or <c>"dns-01"</c>.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>URL to POST an empty object to, triggering CA-side validation.</summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    /// <summary>The challenge token — combined with the account key thumbprint to form the key authorization.</summary>
    [JsonPropertyName("token")]
    public required string Token { get; init; }
}
