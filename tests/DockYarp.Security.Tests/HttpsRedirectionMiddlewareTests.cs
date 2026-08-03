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
            new FakeCertificateAvailability(available: true),
            new SecurityHeadersOptions());
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

    /// <summary>An HTTPS request to a nohttps host is refused with 404.</summary>
    [Test]
    public async Task HttpsRefusedForNoHttpsHost()
    {
        HttpsRedirectionMiddleware middleware = Middleware(HttpsMethod.NoHttps, certificateAvailable: true);
        DefaultHttpContext context = SecurityTestHelpers.Context("https", "app.local", "/");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        nextCalled.Should().BeFalse();
    }

    /// <summary>A non-GET request is redirected with a method-preserving 308 (no method downgrade).</summary>
    [Test]
    public async Task NonGetRequestRedirectedWith308()
    {
        HttpsRedirectionMiddleware middleware = Middleware(HttpsMethod.Redirect, certificateAvailable: true);
        DefaultHttpContext context = SecurityTestHelpers.Context("http", "app.local", "/orders");
        context.Request.Method = "POST";

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.StatusCode.Should().Be(StatusCodes.Status308PermanentRedirect);
        context.Response.Headers.Location.ToString().Should().Be("https://app.local/orders");
    }

    /// <summary>With TrustDefaultCert=false, an HTTPS request to a host with no real certificate is refused with 500.</summary>
    [Test]
    public async Task HttpsRefusedWhenDefaultCertNotTrusted()
    {
        HttpsRedirectionMiddleware middleware = Middleware(
            HttpsMethod.NoRedirect, certificateAvailable: false, new SecurityHeadersOptions { TrustDefaultCert = false });
        DefaultHttpContext context = SecurityTestHelpers.Context("https", "app.local", "/");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        nextCalled.Should().BeFalse();
    }

    /// <summary>With TrustDefaultCert=false, an HTTPS request to a host that has a real certificate is served.</summary>
    [Test]
    public async Task HttpsServedWhenCertificateAvailableEvenIfDefaultNotTrusted()
    {
        HttpsRedirectionMiddleware middleware = Middleware(
            HttpsMethod.NoRedirect, certificateAvailable: true, new SecurityHeadersOptions { TrustDefaultCert = false });
        DefaultHttpContext context = SecurityTestHelpers.Context("https", "app.local", "/");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    /// <summary>With EnableHttpOnMissingCert=false, a redirecting host is redirected even without a certificate.</summary>
    [Test]
    public async Task RedirectForcedWhenHttpOnMissingCertDisabled()
    {
        HttpsRedirectionMiddleware middleware = Middleware(
            HttpsMethod.Redirect, certificateAvailable: false, new SecurityHeadersOptions { EnableHttpOnMissingCert = false });
        DefaultHttpContext context = SecurityTestHelpers.Context("http", "app.local", "/orders");

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.StatusCode.Should().Be(StatusCodes.Status308PermanentRedirect);
        context.Response.Headers.Location.ToString().Should().Be("https://app.local/orders");
    }

    /// <summary>A redirecting host with EXTERNAL_HTTPS_PORT redirects to that port (the default 443 is omitted).</summary>
    [Test]
    public async Task RedirectUsesExternalHttpsPort()
    {
        RouteConfigStore store = SecurityTestHelpers.StoreWith(new RouteRule
        {
            HostPattern = "app.local",
            ClusterId = "app",
            Tls = new HostTlsMetadata
            {
                CertificateHost = "app.local",
                Method = HttpsMethod.Redirect,
                ExternalHttpsPort = 8443,
            },
        });
        HttpsRedirectionMiddleware middleware = new(
            new RouteLookup(store, new RoutingOptions()),
            new FakeCertificateAvailability(available: true),
            new SecurityHeadersOptions());
        DefaultHttpContext context = SecurityTestHelpers.Context("http", "app.local", "/orders");

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.Headers.Location.ToString().Should().Be("https://app.local:8443/orders");
    }

    /// <summary>A per-host TRUST_DEFAULT_CERT=false refuses HTTPS (500) even when the global default trusts it.</summary>
    [Test]
    public async Task PerHostTrustDefaultCertFalseRefusesHttps()
    {
        RouteConfigStore store = SecurityTestHelpers.StoreWith(new RouteRule
        {
            HostPattern = "app.local",
            ClusterId = "app",
            Tls = new HostTlsMetadata
            {
                CertificateHost = "app.local",
                Method = HttpsMethod.NoRedirect,
                TrustDefaultCert = false,
            },
        });
        HttpsRedirectionMiddleware middleware = new(
            new RouteLookup(store, new RoutingOptions()),
            new FakeCertificateAvailability(available: false),
            new SecurityHeadersOptions()); // global TrustDefaultCert defaults to true
        DefaultHttpContext context = SecurityTestHelpers.Context("https", "app.local", "/");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        nextCalled.Should().BeFalse();
    }

    /// <summary>A per-host ENABLE_HTTP_ON_MISSING_CERT=false forces the redirect even without a certificate.</summary>
    [Test]
    public async Task PerHostHttpOnMissingCertFalseForcesRedirect()
    {
        RouteConfigStore store = SecurityTestHelpers.StoreWith(new RouteRule
        {
            HostPattern = "app.local",
            ClusterId = "app",
            Tls = new HostTlsMetadata
            {
                CertificateHost = "app.local",
                Method = HttpsMethod.Redirect,
                EnableHttpOnMissingCert = false,
            },
        });
        HttpsRedirectionMiddleware middleware = new(
            new RouteLookup(store, new RoutingOptions()),
            new FakeCertificateAvailability(available: false),
            new SecurityHeadersOptions()); // global EnableHttpOnMissingCert defaults to true
        DefaultHttpContext context = SecurityTestHelpers.Context("http", "app.local", "/orders");

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.StatusCode.Should().Be(StatusCodes.Status308PermanentRedirect);
    }

    private static HttpsRedirectionMiddleware Middleware(
        HttpsMethod method, bool certificateAvailable, SecurityHeadersOptions? options = null)
    {
        RouteConfigStore store = SecurityTestHelpers.StoreWith(new RouteRule
        {
            HostPattern = "app.local",
            ClusterId = "app",
            Tls = new HostTlsMetadata { CertificateHost = "app.local", Method = method },
        });
        return new HttpsRedirectionMiddleware(
            new RouteLookup(store, new RoutingOptions()),
            new FakeCertificateAvailability(certificateAvailable),
            options ?? new SecurityHeadersOptions());
    }

    private sealed class FakeCertificateAvailability(bool available) : ICertificateAvailability
    {
        public bool IsAvailable(string host) => available;
    }
}
