namespace DockYarp.App.Observability;

using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using DockYarp.AdminApi;
using DockYarp.Tls;

/// <summary>Exposes the TLS certificate store to the admin API as sanitized views.</summary>
/// <param name="store">The certificate store.</param>
public sealed class CertificateInventoryAdapter(ICertificateStore store) : ICertificateInventory
{
    /// <inheritdoc />
    public IReadOnlyList<AdminApiModels.CertView> List() =>
        [.. store.List().Select(info =>
            new AdminApiModels.CertView(info.Host, info.NotAfter.ToString("O", CultureInfo.InvariantCulture)))];
}
