namespace DockYarp.Security.Tests;

using System.Threading.Tasks;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;

/// <summary>Tests for <see cref="SecurityHeadersMiddleware"/>.</summary>
public sealed class SecurityHeadersMiddlewareTests
{
    /// <summary>Baseline headers are applied and the pipeline continues.</summary>
    [Test]
    public async Task BaselineHeadersApplied()
    {
        SecurityHeadersMiddleware middleware = new(new SecurityHeadersOptions());
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
        SecurityHeadersMiddleware middleware = new(new SecurityHeadersOptions());
        DefaultHttpContext https = SecurityTestHelpers.Context("https", "app.local", "/");
        DefaultHttpContext http = SecurityTestHelpers.Context("http", "app.local", "/");

        await middleware.InvokeAsync(https, _ => Task.CompletedTask);
        await middleware.InvokeAsync(http, _ => Task.CompletedTask);

        https.Response.Headers.ContainsKey("Strict-Transport-Security").Should().BeTrue();
        http.Response.Headers.ContainsKey("Strict-Transport-Security").Should().BeFalse();
    }
}
