namespace DockYarp.Tls;

/// <summary>A host that should have a certificate, with its optional contact email.</summary>
/// <param name="Host">The host to certify.</param>
/// <param name="Email">The contact email declared for the host, if any.</param>
public sealed record DesiredCertificate(string Host, string? Email);
