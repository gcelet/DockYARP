namespace DockYarp.Tls.Acme;

using System.Text.Json.Serialization;

/// <summary>Request body for account creation (RFC 8555 §7.3).</summary>
internal sealed record AcmeNewAccountRequest
{
    /// <summary>Whether the caller agrees to the CA's terms of service.</summary>
    [JsonPropertyName("termsOfServiceAgreed")]
    public required bool TermsOfServiceAgreed { get; init; }

    /// <summary>Contact URIs (e.g. <c>mailto:</c>), matching Certes' own single-email usage.</summary>
    [JsonPropertyName("contact")]
    public required string[] Contact { get; init; }
}
