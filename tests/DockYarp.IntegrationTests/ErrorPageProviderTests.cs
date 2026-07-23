namespace DockYarp.IntegrationTests;

using System.IO.Abstractions.TestingHelpers;

using AwesomeAssertions;

using DockYarp.App.ErrorPages;

/// <summary>Tests for <see cref="ErrorPageProvider"/>.</summary>
public sealed class ErrorPageProviderTests
{
    /// <summary>Pages named by status code are loaded from the directory.</summary>
    [Test]
    public void LoadsPagesByStatusCode()
    {
        MockFileSystem fileSystem = new();
        string directory = fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), "errors");
        fileSystem.AddFile(fileSystem.Path.Combine(directory, "404.html"), new MockFileData("<h1>Not found</h1>"));
        fileSystem.AddFile(fileSystem.Path.Combine(directory, "readme.txt"), new MockFileData("ignored"));

        ErrorPageProvider provider = new(new ErrorPagesOptions { Directory = directory }, fileSystem);

        provider.HasPages.Should().BeTrue();
        provider.TryGetPage(404, out string? html).Should().BeTrue();
        html.Should().Be("<h1>Not found</h1>");
        provider.TryGetPage(503, out _).Should().BeFalse();
    }

    /// <summary>A missing directory yields no pages.</summary>
    [Test]
    public void MissingDirectoryYieldsNoPages()
    {
        ErrorPageProvider provider = new(new ErrorPagesOptions { Directory = null }, new MockFileSystem());

        provider.HasPages.Should().BeFalse();
    }
}
