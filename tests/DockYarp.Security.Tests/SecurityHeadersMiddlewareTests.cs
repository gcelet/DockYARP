namespace DockYarp.Security.Tests;

using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Core.Configuration;
using DockYarp.Core.Models;

using Microsoft.AspNetCore.Http;

/// <summary>Tests for <see cref="SecurityHeadersMiddleware"/>.</summary>
public sealed class SecurityHeadersMiddlewareTests
{
    /// <summary>Baseline headers are applied and the pipeline continues.</summary>
    [Test]
    public async Task BaselineHeadersApplied()
    {
        SecurityHeadersMiddleware middleware = new(new SecurityHeadersOptions(), Lookup());
        DefaultHttpContext context = SecurityTestHelpers.Context("http", "app.local", "/");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        context.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
        nextCalled.Should().BeTrue();
    }

    /// <summary>HSTS is emitted on HTTPS but not on HTTP.</summary>
    [Test]
    public async Task HstsOnlyOnHttps()
    {
        SecurityHeadersMiddleware middleware = new(new SecurityHeadersOptions(), Lookup());
        DefaultHttpContext https = SecurityTestHelpers.Context("https", "app.local", "/");
        DefaultHttpContext http = SecurityTestHelpers.Context("http", "app.local", "/");

        await middleware.InvokeAsync(https, _ => Task.CompletedTask);
        await middleware.InvokeAsync(http, _ => Task.CompletedTask);

        https.Response.Headers.ContainsKey("Strict-Transport-Security").Should().BeTrue();
        http.Response.Headers.ContainsKey("Strict-Transport-Security").Should().BeFalse();
    }

    /// <summary>The preload directive is emitted when enabled.</summary>
    [Test]
    public async Task PreloadDirectiveEmitted()
    {
        SecurityHeadersMiddleware middleware = new(new SecurityHeadersOptions { HstsPreload = true }, Lookup());
        DefaultHttpContext context = SecurityTestHelpers.Context("https", "app.local", "/");

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.Headers["Strict-Transport-Security"].ToString().Should().Contain("preload");
    }

    /// <summary>A per-host HSTS override of "off" suppresses the header for that host.</summary>
    [Test]
    public async Task PerHostOffSuppressesHsts()
    {
        RouteLookup lookup = Lookup(new RouteRule
        {
            HostPattern = "app.local",
            ClusterId = "app",
            Tls = new HostTlsMetadata { CertificateHost = "app.local", Hsts = "off" },
        });
        SecurityHeadersMiddleware middleware = new(new SecurityHeadersOptions(), lookup);
        DefaultHttpContext context = SecurityTestHelpers.Context("https", "app.local", "/");

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.Headers.ContainsKey("Strict-Transport-Security").Should().BeFalse();
    }

    private static RouteLookup Lookup(params RouteRule[] routes) =>
        new(SecurityTestHelpers.StoreWith(routes), new RoutingOptions());
}
