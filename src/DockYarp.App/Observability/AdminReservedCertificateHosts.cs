namespace DockYarp.App.Observability;

using System.Collections.Generic;

using DockYarp.AdminApi;
using DockYarp.Tls;

/// <summary>Contributes the dedicated admin host to certificate provisioning when opted in.</summary>
/// <remarks>Bridges <c>AdminApi:Host</c>/<c>AdminApi:LetsEncrypt</c> to the TLS provisioning loop without the TLS
/// library depending on the admin subsystem. Contact email falls back to <c>Tls:ContactEmail</c>, as routes do.</remarks>
/// <param name="admin">The admin API options (host + ACME opt-in + contact).</param>
/// <param name="tls">The TLS options (for the contact-email fallback).</param>
internal sealed class AdminReservedCertificateHosts(AdminApiOptions admin, TlsOptions tls) : IReservedCertificateHosts
{
    /// <inheritdoc />
    public IReadOnlyList<DesiredCertificate> Reserved =>
        admin is { LetsEncrypt: true, Host: { Length: > 0 } host }
            ? [new DesiredCertificate(host, admin.ContactEmail ?? tls.ContactEmail)]
            : [];
}
