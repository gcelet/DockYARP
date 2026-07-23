namespace DockYarp.IntegrationTests;

using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.App.ErrorPages;

using Microsoft.AspNetCore.Http;

/// <summary>Tests for <see cref="ErrorPageMiddleware"/>.</summary>
public sealed class ErrorPageMiddlewareTests
{
    /// <summary>A configured page is written as the body of a bodiless error response.</summary>
    [Test]
    public async Task WritesConfiguredPageForError()
    {
        ErrorPageMiddleware middleware = new(Provider(("404.html", "<h1>Not found</h1>")));
        DefaultHttpContext context = Context();

        await middleware.InvokeAsync(context, ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });

        BodyOf(context).Should().Be("<h1>Not found</h1>");
        context.Response.ContentType.Should().StartWith("text/html");
    }

    /// <summary>Without a page for the status code, the response body is left empty.</summary>
    [Test]
    public async Task LeavesResponseWhenNoPage()
    {
        ErrorPageMiddleware middleware = new(Provider());
        DefaultHttpContext context = Context();

        await middleware.InvokeAsync(context, ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });

        BodyOf(context).Should().BeEmpty();
    }

    /// <summary>A response that already carries a body is left unchanged.</summary>
    [Test]
    public async Task LeavesResponseWithExistingBody()
    {
        ErrorPageMiddleware middleware = new(Provider(("404.html", "<h1>Not found</h1>")));
        DefaultHttpContext context = Context();

        await middleware.InvokeAsync(context, ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            ctx.Response.ContentLength = 12;
            return Task.CompletedTask;
        });

        BodyOf(context).Should().BeEmpty();
    }

    private static ErrorPageProvider Provider(params (string Name, string Content)[] pages)
    {
        MockFileSystem fileSystem = new();
        string directory = fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), "errors");
        foreach ((string name, string content) in pages)
        {
            fileSystem.AddFile(fileSystem.Path.Combine(directory, name), new MockFileData(content));
        }

        return new ErrorPageProvider(new ErrorPagesOptions { Directory = directory }, fileSystem);
    }

    private static DefaultHttpContext Context()
    {
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string BodyOf(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using StreamReader reader = new(context.Response.Body);
        return reader.ReadToEnd();
    }
}
