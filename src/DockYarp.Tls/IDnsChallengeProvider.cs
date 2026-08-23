namespace DockYarp.Tls;

using System.Threading;
using System.Threading.Tasks;

/// <summary>Publishes and removes the TXT record an ACME DNS-01 challenge validates against.</summary>
/// <remarks>The concrete implementation performs the real DNS exchange; it is faked in tests.</remarks>
public interface IDnsChallengeProvider
{
    /// <summary>Publishes a TXT record.</summary>
    /// <param name="fqdn">The fully-qualified record name (e.g. <c>_acme-challenge.example.com</c>).</param>
    /// <param name="value">The TXT record's text value.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task PublishTxtRecordAsync(string fqdn, string value, CancellationToken cancellationToken);

    /// <summary>Removes a previously published TXT record.</summary>
    /// <param name="fqdn">The fully-qualified record name (e.g. <c>_acme-challenge.example.com</c>).</param>
    /// <param name="value">The exact TXT record's text value to remove.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task RemoveTxtRecordAsync(string fqdn, string value, CancellationToken cancellationToken);
}
