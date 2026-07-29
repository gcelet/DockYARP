namespace DockYarp.Security.Tests;

using System.Net;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Core.Configuration;
using DockYarp.Core.Models;

using Microsoft.AspNetCore.Http;

/// <summary>Tests for <see cref="NetworkAccessMiddleware"/>.</summary>
public sealed class NetworkAccessMiddlewareTests
{
    /// <summary>An internal-only route denies a client outside the internal ranges with 403.</summary>
    [Test]
    public async Task InternalOnlyDeniesExternalClient()
    {
        NetworkAccessMiddleware middleware = Middleware(internalOnly: true);
        DefaultHttpContext context = Context("203.0.113.7");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        nextCalled.Should().BeFalse();
    }

    /// <summary>An internal-only route serves a client within the internal ranges.</summary>
    [Test]
    public async Task InternalOnlyServesInternalClient()
    {
        NetworkAccessMiddleware middleware = Middleware(internalOnly: true);
        DefaultHttpContext context = Context("10.1.2.3");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
    }

    /// <summary>An IPv4-mapped IPv6 client IP is normalized and matched against the IPv4 ranges.</summary>
    [Test]
    public async Task Ipv4MappedInternalClientIsServed()
    {
        NetworkAccessMiddleware middleware = Middleware(internalOnly: true);
        DefaultHttpContext context = Context("::ffff:10.1.2.3");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
    }

    /// <summary>An unknown client IP is treated as external and denied.</summary>
    [Test]
    public async Task InternalOnlyDeniesUnknownClient()
    {
        NetworkAccessMiddleware middleware = Middleware(internalOnly: true);
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("app.local");
        context.Request.Path = "/";
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        nextCalled.Should().BeFalse();
    }

    /// <summary>A customized internal range governs enforcement.</summary>
    [Test]
    public async Task CustomRangeIsHonored()
    {
        NetworkAccessMiddleware middleware = new(
            Lookup(internalOnly: true), new SecurityHeadersOptions { InternalRanges = ["203.0.113.0/24"] });
        DefaultHttpContext allowed = Context("203.0.113.7");
        DefaultHttpContext denied = Context("10.1.2.3");
        bool allowedNext = false;

        await middleware.InvokeAsync(allowed, _ =>
        {
            allowedNext = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(denied, _ => Task.CompletedTask);

        allowedNext.Should().BeTrue();
        denied.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    /// <summary>A route not marked internal-only serves any client.</summary>
    [Test]
    public async Task NonInternalRouteServesAnyClient()
    {
        NetworkAccessMiddleware middleware = Middleware(internalOnly: false);
        DefaultHttpContext context = Context("203.0.113.7");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
    }

    private static DefaultHttpContext Context(string clientIp)
    {
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("app.local");
        context.Request.Path = "/";
        context.Connection.RemoteIpAddress = IPAddress.Parse(clientIp);
        return context;
    }

    private static NetworkAccessMiddleware Middleware(bool internalOnly) =>
        new(Lookup(internalOnly), new SecurityHeadersOptions());

    private static RouteLookup Lookup(bool internalOnly) =>
        new(
            SecurityTestHelpers.StoreWith(new RouteRule
            {
                HostPattern = "app.local",
                ClusterId = "app",
                InternalOnly = internalOnly,
            }),
            new RoutingOptions());
}
