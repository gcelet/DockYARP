namespace DockYarp.Tls.Acme;

using System.Text.Json.Serialization;

/// <summary>An order resource (RFC 8555 §7.1.3).</summary>
internal sealed record AcmeOrder
{
    /// <summary>The order's lifecycle status.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>Authorization URLs to fetch and complete, one per identifier.</summary>
    [JsonPropertyName("authorizations")]
    public required string[] Authorizations { get; init; }

    /// <summary>URL to POST the CSR to once every authorization is valid.</summary>
    [JsonPropertyName("finalize")]
    public required string Finalize { get; init; }

    /// <summary>URL to fetch the issued certificate chain from, present once <see cref="Status"/> is valid.</summary>
    [JsonPropertyName("certificate")]
    public string? Certificate { get; init; }
}
