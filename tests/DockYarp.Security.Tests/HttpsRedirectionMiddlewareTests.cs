namespace DockYarp.Security.Tests;

using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Core.Models;
using DockYarp.Core.Stores;

using Microsoft.AspNetCore.Http;

/// <summary>Tests for <see cref="HttpsRedirectionMiddleware"/>.</summary>
public sealed class HttpsRedirectionMiddlewareTests
{
    /// <summary>An enforced host is redirected to HTTPS with a 308.</summary>
    [Test]
    public async Task RedirectsWhenEnforced()
    {
        RouteConfigStore store = SecurityTestHelpers.StoreWith(new RouteRule
        {
            HostPattern = "app.local",
            ClusterId = "app",
            Tls = new HostTlsMetadata { CertificateHost = "app.local", EnforceHttps = true },
        });
        HttpsRedirectionMiddleware middleware = new(new RouteLookup(store));
        DefaultHttpContext context = SecurityTestHelpers.Context("http", "app.local", "/orders");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        context.Response.StatusCode.Should().Be(StatusCodes.Status308PermanentRedirect);
        context.Response.Headers.Location.ToString().Should().Be("https://app.local/orders");
        nextCalled.Should().BeFalse();
    }

    /// <summary>A host without enforcement is not redirected.</summary>
    [Test]
    public async Task NoRedirectWhenNotEnforced()
    {
        RouteConfigStore store = SecurityTestHelpers.StoreWith(new RouteRule { HostPattern = "app.local", ClusterId = "app" });
        HttpsRedirectionMiddleware middleware = new(new RouteLookup(store));
        DefaultHttpContext context = SecurityTestHelpers.Context("http", "app.local", "/");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }
}
