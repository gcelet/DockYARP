namespace DockYarp.Tls.Acme;

using System.Text.Json.Serialization;

/// <summary>Request body for order creation (RFC 8555 §7.4) — always a single identifier.</summary>
internal sealed record AcmeNewOrderRequest
{
    /// <summary>The single host being ordered.</summary>
    [JsonPropertyName("identifiers")]
    public required AcmeIdentifier[] Identifiers { get; init; }
}
