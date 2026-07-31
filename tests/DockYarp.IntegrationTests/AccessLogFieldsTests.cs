namespace DockYarp.IntegrationTests;

using System.Collections.Generic;
using System.Linq;

using AwesomeAssertions;

using DockYarp.App.Observability;

using Microsoft.AspNetCore.Http;

/// <summary>Tests for <see cref="AccessLogFields"/>.</summary>
public sealed class AccessLogFieldsTests
{
    private static readonly IReadOnlyList<KeyValuePair<string, object>> Catalog =
    [
        new("Method", "GET"),
        new("Host", "app.local"),
        new("Path", "/api/orders"),
        new("StatusCode", 200),
        new("ElapsedMs", 12.5),
    ];

    /// <summary>A custom selection returns exactly those fields, in the configured order.</summary>
    [Test]
    public void SelectsExactlyTheConfiguredFieldsInOrder()
    {
        IReadOnlyList<KeyValuePair<string, object>> selected = AccessLogFields.Select(Catalog, ["Path", "StatusCode"]);

        selected.Select(entry => entry.Key).Should().Equal("Path", "StatusCode");
        selected.Select(entry => entry.Value).Should().Equal("/api/orders", 200);
    }

    /// <summary>Unknown field names are skipped; matching is case-insensitive and emits the canonical name.</summary>
    [Test]
    public void SkipsUnknownFieldsAndMatchesCaseInsensitively()
    {
        IReadOnlyList<KeyValuePair<string, object>> selected =
            AccessLogFields.Select(Catalog, ["path", "bogus", "method"]);

        selected.Select(entry => entry.Key).Should().Equal("Path", "Method");
    }

    /// <summary>The catalog built from a request exposes the canonical field names.</summary>
    [Test]
    public void BuildExposesCanonicalFields()
    {
        DefaultHttpContext context = new();
        context.Request.Method = "POST";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("app.local");
        context.Request.Path = "/orders";
        context.Response.StatusCode = 201;

        IReadOnlyList<KeyValuePair<string, object>> catalog = AccessLogFields.Build(context, 8.0);

        catalog.Select(entry => entry.Key).Should().Equal(AccessLogFields.Names);
        catalog.Single(entry => entry.Key == "Method").Value.Should().Be("POST");
        catalog.Single(entry => entry.Key == "StatusCode").Value.Should().Be(201);
    }
}
