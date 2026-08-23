namespace DockYarp.Tls;

using DockYarp.Core.Models;

/// <summary>A host that should have a certificate, with its optional contact email.</summary>
/// <param name="Host">The host to certify.</param>
/// <param name="Email">The contact email declared for the host, if any.</param>
/// <param name="ChallengeType">The ACME challenge type to provision the host with (default HTTP-01).</param>
public sealed record DesiredCertificate(string Host, string? Email, AcmeChallengeType ChallengeType = AcmeChallengeType.Http01);
