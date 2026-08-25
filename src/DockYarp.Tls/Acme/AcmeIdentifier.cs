namespace DockYarp.Tls.Acme;

using System.Text.Json.Serialization;

/// <summary>An order identifier (RFC 8555 §9.7.7) — DockYarp only ever uses the <c>dns</c> type.</summary>
internal sealed record AcmeIdentifier
{
    /// <summary>The identifier type, always <c>"dns"</c> here.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>The host name (or <c>*.host</c> for a wildcard DNS-01 order).</summary>
    [JsonPropertyName("value")]
    public required string Value { get; init; }
}
