namespace DockYarp.Core.Models;

/// <summary>Selects which ACME challenge type provisions a host's certificate (<c>DOCKYARP_ACME_CHALLENGE</c>).</summary>
public enum AcmeChallengeType
{
    /// <summary>HTTP-01 (the default) — cannot issue wildcard certificates.</summary>
    Http01,

    /// <summary>DNS-01 — required for a wildcard <c>CertificateHost</c>, resolved via a configured DNS provider.</summary>
    Dns01,
}
