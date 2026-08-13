namespace DockYarp.Tls;

using System.Collections.Generic;

/// <summary>Default <see cref="IReservedCertificateHosts"/> that reserves nothing.</summary>
/// <remarks>Registered by <c>AddDockYarpTls</c> so the TLS library provisions only route-derived hosts unless a host
/// application supplies its own implementation.</remarks>
public sealed class NoReservedCertificateHosts : IReservedCertificateHosts
{
    /// <inheritdoc />
    public IReadOnlyList<DesiredCertificate> Reserved => [];
}
