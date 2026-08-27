namespace DockYarp.Tls.Acme;

using System.Text.Json.Serialization;

/// <summary>Request body to revoke a certificate (RFC 8555 §7.6) — the certificate, base64url-encoded DER.
/// The optional <c>reason</c> field is deliberately omitted (see add-acme-certificate-revocation's design).</summary>
internal sealed record AcmeRevokeCertificateRequest
{
    /// <summary>The base64url-encoded DER-encoded certificate to revoke.</summary>
    [JsonPropertyName("certificate")]
    public required string Certificate { get; init; }
}
