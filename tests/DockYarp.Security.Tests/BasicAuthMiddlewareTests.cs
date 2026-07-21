namespace DockYarp.Security.Tests;

using System;
using System.Text;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Core.Models;
using DockYarp.Core.Stores;

using Microsoft.AspNetCore.Http;

/// <summary>Tests for <see cref="BasicAuthMiddleware"/>.</summary>
public sealed class BasicAuthMiddlewareTests
{
    /// <summary>A protected route without credentials returns 401 with a challenge.</summary>
    [Test]
    public async Task MissingCredentialsRejected()
    {
        BasicAuthMiddleware middleware = new(new RouteLookup(ProtectedStore()));
        DefaultHttpContext context = SecurityTestHelpers.Context("https", "app.local", "/");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.Headers.WWWAuthenticate.ToString().Should().StartWith("Basic");
        nextCalled.Should().BeFalse();
    }

    /// <summary>Valid credentials let the request proceed.</summary>
    [Test]
    public async Task ValidCredentialsAccepted()
    {
        BasicAuthMiddleware middleware = new(new RouteLookup(ProtectedStore()));
        DefaultHttpContext context = SecurityTestHelpers.Context("https", "app.local", "/");
        context.Request.Headers.Authorization = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:secret"));
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
    }

    /// <summary>An unprotected route is not challenged.</summary>
    [Test]
    public async Task UnprotectedRouteNotChallenged()
    {
        RouteConfigStore store = SecurityTestHelpers.StoreWith(new RouteRule { HostPattern = "app.local", ClusterId = "app" });
        BasicAuthMiddleware middleware = new(new RouteLookup(store));
        DefaultHttpContext context = SecurityTestHelpers.Context("https", "app.local", "/");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
    }

    private static RouteConfigStore ProtectedStore() =>
        SecurityTestHelpers.StoreWith(new RouteRule
        {
            HostPattern = "app.local",
            ClusterId = "app",
            Auth = new BasicAuthCredentials { Username = "admin", Password = "secret" },
        });
}
