namespace DockYarp.IntegrationTests;

using System.IO.Abstractions.TestingHelpers;
using System.Linq;

using AwesomeAssertions;

using DockYarp.App.StaticConfig;
using DockYarp.Core.Configuration;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Tests for <see cref="StaticConfigProvider"/>.</summary>
public sealed class StaticConfigProviderTests
{
    private const string Json =
        """
        {
          "clusters": [ { "id": "api", "addresses": ["http://10.0.0.9:8080"], "loadBalancing": "least-requests" } ],
          "routes": [ { "host": "api.local", "path": "/v1", "cluster": "api", "priority": 5 } ]
        }
        """;

    /// <summary>A static file is parsed into routes and clusters.</summary>
    [Test]
    public void LoadsRoutesAndClusters()
    {
        MockFileSystem fileSystem = new();
        string path = fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), "config.json");
        fileSystem.AddFile(path, new MockFileData(Json));

        ConfigContribution contribution = Provider(fileSystem, path).GetContribution();

        contribution.Source.Should().Be(ConfigSource.Static);
        contribution.Routes.Single().HostPattern.Should().Be("api.local");
        contribution.Routes.Single().ClusterId.Should().Be("api");
        contribution.Clusters.Single().Id.Should().Be("api");
        contribution.Clusters.Single().Endpoints.Single().Address.Should().Be("http://10.0.0.9:8080");
    }

    /// <summary>With no path configured the contribution is empty.</summary>
    [Test]
    public void MissingPathYieldsEmpty()
    {
        ConfigContribution contribution = Provider(new MockFileSystem(), path: null).GetContribution();

        contribution.Routes.Should().BeEmpty();
        contribution.Clusters.Should().BeEmpty();
    }

    /// <summary>An unreadable/invalid file fails open to an empty contribution.</summary>
    [Test]
    public void InvalidJsonYieldsEmpty()
    {
        MockFileSystem fileSystem = new();
        string path = fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), "config.json");
        fileSystem.AddFile(path, new MockFileData("{ not valid json"));

        ConfigContribution contribution = Provider(fileSystem, path).GetContribution();

        contribution.Routes.Should().BeEmpty();
        contribution.Clusters.Should().BeEmpty();
    }

    private static StaticConfigProvider Provider(MockFileSystem fileSystem, string? path) =>
        new(new StaticConfigOptions { Path = path }, fileSystem, NullLogger<StaticConfigProvider>.Instance);
}
