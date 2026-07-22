namespace DockYarp.Security.Tests;

using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Core.Configuration;
using DockYarp.Core.Models;
using DockYarp.Core.Stores;

using Microsoft.AspNetCore.Http;

/// <summary>Tests for <see cref="HttpsRedirectionMiddleware"/>.</summary>
public sealed class HttpsRedirectionMiddlewareTests
{
    /// <summary>A redirecting host with an available certificate is redirected to HTTPS with a 308.</summary>
    [Test]
    public async Task RedirectsWhenMethodRedirectsAndCertificateAvailable()
    {
        HttpsRedirectionMiddleware middleware = Middleware(HttpsMethod.Redirect, certificateAvailable: true);
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

    /// <summary>A redirecting host is not redirected while no certificate is available yet.</summary>
    [Test]
    public async Task NoRedirectWhenCertificateUnavailable()
    {
        HttpsRedirectionMiddleware middleware = Middleware(HttpsMethod.Redirect, certificateAvailable: false);
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

    /// <summary>A host whose method is noredirect is served over HTTP even with a certificate available.</summary>
    [Test]
    public async Task NoRedirectForNoRedirectMethod()
    {
        HttpsRedirectionMiddleware middleware = Middleware(HttpsMethod.NoRedirect, certificateAvailable: true);
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

    /// <summary>A host without TLS metadata is not redirected.</summary>
    [Test]
    public async Task NoRedirectWhenNoTlsMetadata()
    {
        RouteConfigStore store = SecurityTestHelpers.StoreWith(new RouteRule { HostPattern = "app.local", ClusterId = "app" });
        HttpsRedirectionMiddleware middleware = new(
            new RouteLookup(store, new RoutingOptions()),
            new FakeCertificateAvailability(available: true));
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

    private static HttpsRedirectionMiddleware Middleware(HttpsMethod method, bool certificateAvailable)
    {
        RouteConfigStore store = SecurityTestHelpers.StoreWith(new RouteRule
        {
            HostPattern = "app.local",
            ClusterId = "app",
            Tls = new HostTlsMetadata { CertificateHost = "app.local", Method = method },
        });
        return new HttpsRedirectionMiddleware(
            new RouteLookup(store, new RoutingOptions()),
            new FakeCertificateAvailability(certificateAvailable));
    }

    private sealed class FakeCertificateAvailability(bool available) : ICertificateAvailability
    {
        public bool IsAvailable(string host) => available;
    }
}
