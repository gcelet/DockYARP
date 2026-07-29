namespace DockYarp.Security.Tests;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Core.Configuration;
using DockYarp.Core.Models;
using DockYarp.Core.Stores;

using Microsoft.AspNetCore.Http;

/// <summary>Tests for <see cref="BasicAuthMiddleware"/> (label credentials and htpasswd files).</summary>
public sealed class BasicAuthMiddlewareTests
{
    private string dir = string.Empty;

    /// <summary>Creates a unique htpasswd directory for the test.</summary>
    [SetUp]
    public void SetUp() =>
        dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "dockyarp-htpasswd-tests", Guid.NewGuid().ToString("N"))).FullName;

    /// <summary>Removes the htpasswd directory.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>A protected route without credentials returns 401 with a challenge.</summary>
    [Test]
    public async Task MissingCredentialsRejected()
    {
        BasicAuthMiddleware middleware = Middleware(ProtectedStore());
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

    /// <summary>Valid label credentials let the request proceed.</summary>
    [Test]
    public async Task ValidCredentialsAccepted()
    {
        BasicAuthMiddleware middleware = Middleware(ProtectedStore());
        DefaultHttpContext context = Authenticated("app.local", "/", "admin", "secret");
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
        BasicAuthMiddleware middleware = Middleware(
            SecurityTestHelpers.StoreWith(new RouteRule { HostPattern = "app.local", ClusterId = "app" }));
        DefaultHttpContext context = SecurityTestHelpers.Context("https", "app.local", "/");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
    }

    /// <summary>A valid htpasswd user (bcrypt) on a host-scoped file is accepted.</summary>
    [Test]
    public async Task HtpasswdUserAccepted()
    {
        WriteHtpasswd("app.local", $"alice:{BCrypt.Net.BCrypt.HashPassword("wonderland")}");
        BasicAuthMiddleware middleware = Middleware(
            SecurityTestHelpers.StoreWith(new RouteRule { HostPattern = "app.local", ClusterId = "app" }));
        DefaultHttpContext context = Authenticated("app.local", "/", "alice", "wonderland");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
    }

    /// <summary>A wrong htpasswd password is rejected with 401.</summary>
    [Test]
    public async Task HtpasswdWrongPasswordRejected()
    {
        WriteHtpasswd("app.local", $"alice:{BCrypt.Net.BCrypt.HashPassword("wonderland")}");
        BasicAuthMiddleware middleware = Middleware(
            SecurityTestHelpers.StoreWith(new RouteRule { HostPattern = "app.local", ClusterId = "app" }));
        DefaultHttpContext context = Authenticated("app.local", "/", "alice", "wrong");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        nextCalled.Should().BeFalse();
    }

    /// <summary>A path-scoped htpasswd file protects only its path, not the host root.</summary>
    [Test]
    public async Task PathScopedHtpasswdOnlyProtectsItsPath()
    {
        WriteHtpasswd($"app.local_{PathHash("/api")}", $"alice:{BCrypt.Net.BCrypt.HashPassword("wonderland")}");
        BasicAuthMiddleware middleware = Middleware(SecurityTestHelpers.StoreWith(
            new RouteRule { HostPattern = "app.local", PathPrefix = "/api", ClusterId = "api" },
            new RouteRule { HostPattern = "app.local", PathPrefix = "/", ClusterId = "root" }));

        DefaultHttpContext protectedPath = SecurityTestHelpers.Context("https", "app.local", "/api/x");
        DefaultHttpContext openRoot = SecurityTestHelpers.Context("https", "app.local", "/");
        bool rootNext = false;

        await middleware.InvokeAsync(protectedPath, _ => Task.CompletedTask);
        await middleware.InvokeAsync(openRoot, _ =>
        {
            rootNext = true;
            return Task.CompletedTask;
        });

        protectedPath.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        rootNext.Should().BeTrue();
    }

    /// <summary>An apr1 (Apache MD5-crypt) entry verifies against the Apache documentation known-answer vector.</summary>
    [Test]
    public async Task HtpasswdApr1UserAccepted()
    {
        // Apache "Password Formats" known-answer: password "myPassword", salt "r31.....".
        WriteHtpasswd("app.local", "bob:$apr1$r31.....$HqJZimcKQFAMYayBlzkrA/");
        BasicAuthMiddleware middleware = Middleware(
            SecurityTestHelpers.StoreWith(new RouteRule { HostPattern = "app.local", ClusterId = "app" }));
        DefaultHttpContext ok = Authenticated("app.local", "/", "bob", "myPassword");
        DefaultHttpContext bad = Authenticated("app.local", "/", "bob", "nope");
        bool okNext = false;

        await middleware.InvokeAsync(ok, _ =>
        {
            okNext = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(bad, _ => Task.CompletedTask);

        okNext.Should().BeTrue();
        bad.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    /// <summary>A SHA1 (<c>{SHA}</c>) htpasswd entry is verified against the known digest for "password".</summary>
    [Test]
    public async Task HtpasswdSha1UserAccepted()
    {
        WriteHtpasswd("app.local", "carol:{SHA}W6ph5Mm5Pz8GgiULbPgzG37mj9g=");
        BasicAuthMiddleware middleware = Middleware(
            SecurityTestHelpers.StoreWith(new RouteRule { HostPattern = "app.local", ClusterId = "app" }));
        DefaultHttpContext context = Authenticated("app.local", "/", "carol", "password");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
    }

    /// <summary>An unrecognized hash format never authorizes a request.</summary>
    [Test]
    public async Task HtpasswdUnsupportedHashRejected()
    {
        WriteHtpasswd("app.local", "dave:$md5$deadbeef");
        BasicAuthMiddleware middleware = Middleware(
            SecurityTestHelpers.StoreWith(new RouteRule { HostPattern = "app.local", ClusterId = "app" }));
        DefaultHttpContext context = Authenticated("app.local", "/", "dave", "whatever");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        nextCalled.Should().BeFalse();
    }

    private static string PathHash(string path) =>
        Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(path)));

    private static DefaultHttpContext Authenticated(string host, string path, string user, string password)
    {
        DefaultHttpContext context = SecurityTestHelpers.Context("https", host, path);
        context.Request.Headers.Authorization =
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}"));
        return context;
    }

    private static RouteConfigStore ProtectedStore() =>
        SecurityTestHelpers.StoreWith(new RouteRule
        {
            HostPattern = "app.local",
            ClusterId = "app",
            Auth = new BasicAuthCredentials { Username = "admin", Password = "secret" },
        });

    private void WriteHtpasswd(string fileName, string content) =>
        File.WriteAllText(Path.Combine(dir, fileName), content + "\n");

    private BasicAuthMiddleware Middleware(RouteConfigStore store) =>
        new(
            new RouteLookup(store, new RoutingOptions()),
            new HtpasswdStore(new SecurityHeadersOptions { HtpasswdDirectory = dir }));
}
