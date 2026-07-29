namespace DockYarp.Security.Tests;

using System;
using System.Collections.Generic;
using System.IO;

using AwesomeAssertions;

/// <summary>Tests for <see cref="HtpasswdStore"/> file loading and lookup.</summary>
public sealed class HtpasswdStoreTests
{
    private string dir = string.Empty;

    /// <summary>Creates a unique htpasswd directory for the test.</summary>
    [SetUp]
    public void SetUp() =>
        dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "dockyarp-htpasswd-store", Guid.NewGuid().ToString("N"))).FullName;

    /// <summary>Removes the htpasswd directory.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>A host file is parsed, skipping blank lines and comments, exposing each user's hash.</summary>
    [Test]
    public void HostFileParsesUsersSkippingCommentsAndBlanks()
    {
        File.WriteAllText(Path.Combine(dir, "app.local"), "# comment\n\nalice:hashA\nbob:hashB\n");
        HtpasswdStore store = new(new SecurityHeadersOptions { HtpasswdDirectory = dir });

        IReadOnlyDictionary<string, string>? entries = store.Find("app.local", pathPrefix: null);

        entries.Should().NotBeNull();
        entries!.Should().HaveCount(2);
        entries["alice"].Should().Be("hashA");
        entries["bob"].Should().Be("hashB");
    }

    /// <summary>No htpasswd directory yields no entries.</summary>
    [Test]
    public void MissingDirectoryYieldsNoEntries()
    {
        HtpasswdStore store = new(new SecurityHeadersOptions());

        store.Find("app.local", pathPrefix: null).Should().BeNull();
    }

    /// <summary>An unmatched host yields no entries even when other files exist.</summary>
    [Test]
    public void UnmatchedHostYieldsNoEntries()
    {
        File.WriteAllText(Path.Combine(dir, "app.local"), "alice:hashA\n");
        HtpasswdStore store = new(new SecurityHeadersOptions { HtpasswdDirectory = dir });

        store.Find("other.local", pathPrefix: null).Should().BeNull();
    }
}
