namespace DockYarp.Tls.Tests;

using System.IO;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Tls;

using Microsoft.AspNetCore.Http;

/// <summary>Tests for the HTTP-01 challenge store and middleware.</summary>
public sealed class Http01ChallengeTests
{
    /// <summary>A known token is served with its key authorization.</summary>
    [Test]
    public async Task KnownTokenIsServed()
    {
        Http01ChallengeStore store = new();
        store.Set("tok123", "key-auth-value");
        Http01ChallengeMiddleware middleware = new(store);
        DefaultHttpContext context = ContextFor("/.well-known/acme-challenge/tok123");
        using MemoryStream body = new();
        context.Response.Body = body;

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        ReadBody(body).Should().Be("key-auth-value");
    }

    /// <summary>An unknown token returns 404.</summary>
    [Test]
    public async Task UnknownTokenReturnsNotFound()
    {
        Http01ChallengeMiddleware middleware = new(new Http01ChallengeStore());
        DefaultHttpContext context = ContextFor("/.well-known/acme-challenge/missing");

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    /// <summary>A non-challenge request continues down the pipeline.</summary>
    [Test]
    public async Task OtherPathCallsNext()
    {
        Http01ChallengeMiddleware middleware = new(new Http01ChallengeStore());
        DefaultHttpContext context = ContextFor("/app");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
    }

    private static DefaultHttpContext ContextFor(string path)
    {
        DefaultHttpContext context = new();
        context.Request.Path = path;
        return context;
    }

    private static string ReadBody(MemoryStream body)
    {
        body.Position = 0;
        using StreamReader reader = new(body, leaveOpen: true);
        return reader.ReadToEnd();
    }
}
