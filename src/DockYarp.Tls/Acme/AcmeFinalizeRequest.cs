namespace DockYarp.Tls.Acme;

using System.Text.Json.Serialization;

/// <summary>Request body to finalize an order (RFC 8555 §7.4) — the CSR, base64url-encoded DER.</summary>
internal sealed record AcmeFinalizeRequest
{
    /// <summary>The base64url-encoded DER-encoded PKCS#10 CSR.</summary>
    [JsonPropertyName("csr")]
    public required string Csr { get; init; }
}
