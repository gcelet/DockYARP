namespace DockYarp.Security;

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;

/// <summary>Periodically reloads the htpasswd store so credential-file changes apply without a restart.</summary>
/// <param name="store">The htpasswd store to reload.</param>
/// <param name="options">Security options carrying the reload interval.</param>
public sealed class HtpasswdReloadService(HtpasswdStore store, SecurityHeadersOptions options) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.HtpasswdReloadInterval <= TimeSpan.Zero)
        {
            return;
        }

        using PeriodicTimer timer = new(options.HtpasswdReloadInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                store.Reload();
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested.
        }
    }
}
