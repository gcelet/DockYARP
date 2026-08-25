namespace DockYarp.Tls.Acme;

using System.Text.Json.Serialization;

/// <summary>An RFC 7807 problem-details error body (RFC 8555 §6.7) — every ACME error response.</summary>
internal sealed record AcmeProblemDetails
{
    /// <summary>The URN-style error type, e.g. <c>urn:ietf:params:acme:error:badNonce</c>.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>A human-readable explanation.</summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; init; }
}
