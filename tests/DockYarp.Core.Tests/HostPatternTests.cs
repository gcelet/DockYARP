namespace DockYarp.Core.Tests;

using AwesomeAssertions;

using DockYarp.Core.Routing;

/// <summary>Tests for <see cref="HostPattern"/> classification and matching.</summary>
public sealed class HostPatternTests
{
    /// <summary>An exact pattern matches only that host.</summary>
    [Test]
    public void ExactMatchesOnlyItself()
    {
        HostPattern pattern = HostPattern.Parse("app.local");

        pattern.Kind.Should().Be(HostPatternKind.Exact);
        pattern.Matches("app.local").Should().BeTrue();
        pattern.Matches("APP.LOCAL").Should().BeTrue();
        pattern.Matches("x.app.local").Should().BeFalse();
    }

    /// <summary>A leading wildcard matches a subdomain of any depth but not the apex.</summary>
    [Test]
    public void LeadingWildcardMatchesAnyDepth()
    {
        HostPattern pattern = HostPattern.Parse("*.local");

        pattern.Kind.Should().Be(HostPatternKind.LeadingWildcard);
        pattern.Matches("a.local").Should().BeTrue();
        pattern.Matches("a.b.local").Should().BeTrue();
        pattern.Matches("local").Should().BeFalse();
    }

    /// <summary>A trailing wildcard matches any host beginning with the prefix.</summary>
    [Test]
    public void TrailingWildcardMatchesAnySuffix()
    {
        HostPattern pattern = HostPattern.Parse("app.*");

        pattern.Kind.Should().Be(HostPatternKind.TrailingWildcard);
        pattern.Matches("app.local").Should().BeTrue();
        pattern.Matches("app.example.com").Should().BeTrue();
        pattern.Matches("app").Should().BeFalse();
        pattern.Matches("other.local").Should().BeFalse();
    }

    /// <summary>A ~-prefixed regex matches hosts satisfying the expression and rejects others.</summary>
    [Test]
    public void RegexMatchesExpression()
    {
        HostPattern pattern = HostPattern.Parse(@"~^app-\d+\.example\.com$");

        pattern.Kind.Should().Be(HostPatternKind.Regex);
        pattern.Matches("app-42.example.com").Should().BeTrue();
        pattern.Matches("app-x.example.com").Should().BeFalse();
        pattern.Matches("other.example.com").Should().BeFalse();
    }

    /// <summary>An invalid regex never matches (fails closed rather than throwing).</summary>
    [Test]
    public void InvalidRegexNeverMatches()
    {
        HostPattern pattern = HostPattern.Parse("~^app-[");

        pattern.Kind.Should().Be(HostPatternKind.Regex);
        pattern.Matches("app-42.local").Should().BeFalse();
    }
}
