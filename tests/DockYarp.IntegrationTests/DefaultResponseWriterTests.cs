namespace DockYarp.IntegrationTests;

using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.App.Routing;
using DockYarp.Core.Configuration;

using Microsoft.AspNetCore.Http;

/// <summary>Tests for <see cref="DefaultResponseWriter"/> (the unmatched-request fallback).</summary>
public sealed class DefaultResponseWriterTests
{
    /// <summary>With no redirect location, only the status code is written.</summary>
    [Test]
    public async Task StatusOnlyWhenNoLocation()
    {
        RoutingOptions options = new() { DefaultResponseStatusCode = 404 };
        DefaultHttpContext context = Context("http", "app.local", "/x", queryString: string.Empty);

        await DefaultResponseWriter.WriteAsync(context, options);

        context.Response.StatusCode.Should().Be(404);
        context.Response.Headers.Location.ToString().Should().BeEmpty();
    }

    /// <summary>A redirect location is written with $host/$request_uri substitution.</summary>
    [Test]
    public async Task RedirectWithSubstitution()
    {
        RoutingOptions options = new()
        {
            DefaultResponseStatusCode = 302,
            DefaultResponseLocation = "https://$host$request_uri",
        };
        DefaultHttpContext context = Context("http", "app.local", "/x", "?a=1");

        await DefaultResponseWriter.WriteAsync(context, options);

        context.Response.StatusCode.Should().Be(302);
        context.Response.Headers.Location.ToString().Should().Be("https://app.local/x?a=1");
    }

    /// <summary><c>$$</c> in the template yields a literal <c>$</c>.</summary>
    [Test]
    public async Task DollarEscapeIsLiteral()
    {
        RoutingOptions options = new()
        {
            DefaultResponseStatusCode = 302,
            DefaultResponseLocation = "https://example.test/$$literal",
        };
        DefaultHttpContext context = Context("https", "app.local", "/", queryString: string.Empty);

        await DefaultResponseWriter.WriteAsync(context, options);

        context.Response.Headers.Location.ToString().Should().Be("https://example.test/$literal");
    }

    private static DefaultHttpContext Context(string scheme, string host, string path, string queryString)
    {
        DefaultHttpContext context = new();
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(queryString);
        return context;
    }
}
