namespace DockYarp.IntegrationTests;

using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.App.ReverseProxy;
using DockYarp.Core.Configuration;
using DockYarp.Core.Models;
using DockYarp.Core.Stores;
using DockYarp.Security;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

/// <summary>Tests for <see cref="RequestBodySizeMiddleware"/>.</summary>
public sealed class RequestBodySizeMiddlewareTests
{
    /// <summary>A matched route's limit is applied to the request body-size feature.</summary>
    [Test]
    public async Task AppliesRouteLimit()
    {
        RequestBodySizeMiddleware middleware = Middleware(
            new RouteRule { HostPattern = "app.local", ClusterId = "app", MaxRequestBodySize = 1000 });
        DefaultHttpContext context = Context();
        FakeBodySizeFeature feature = new();
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(feature);

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        feature.MaxRequestBodySize.Should().Be(1000);
    }

    /// <summary>A route without a limit leaves the body-size feature unchanged.</summary>
    [Test]
    public async Task NoLimitLeavesFeatureUnchanged()
    {
        RequestBodySizeMiddleware middleware = Middleware(
            new RouteRule { HostPattern = "app.local", ClusterId = "app" });
        DefaultHttpContext context = Context();
        FakeBodySizeFeature feature = new() { MaxRequestBodySize = 42 };
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(feature);

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        feature.MaxRequestBodySize.Should().Be(42);
    }

    private static RequestBodySizeMiddleware Middleware(RouteRule route)
    {
        RouteConfigStore store = new();
        store.Apply([route], []);
        return new RequestBodySizeMiddleware(new RouteLookup(store, new RoutingOptions()));
    }

    private static DefaultHttpContext Context()
    {
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("app.local");
        context.Request.Path = "/";
        return context;
    }

    private sealed class FakeBodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;

        public long? MaxRequestBodySize { get; set; }
    }
}
