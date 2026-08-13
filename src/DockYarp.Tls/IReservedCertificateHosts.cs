namespace DockYarp.Tls;

using System.Collections.Generic;

/// <summary>Contributes certificate desires for hosts that are not derived from routes.</summary>
/// <remarks>
/// Lets a higher layer (for example the ASP.NET host, which knows the dedicated admin host) add hosts to the
/// provisioning loop without the TLS library depending on that layer. The default contributes nothing, so TLS
/// provisions exactly the route-derived hosts unless an implementation is registered.
/// </remarks>
public interface IReservedCertificateHosts
{
    /// <summary>Gets the reserved hosts to provision, in addition to the route-derived ones.</summary>
    IReadOnlyList<DesiredCertificate> Reserved { get; }
}
