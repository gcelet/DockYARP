namespace DockYarp.Tls;

using System;

/// <summary>Summary information about a stored certificate.</summary>
/// <param name="Host">The host the certificate is for.</param>
/// <param name="NotAfter">The certificate's expiry.</param>
public sealed record CertificateInfo(string Host, DateTimeOffset NotAfter);
