namespace DockYarp.IntegrationTests;

using System.Collections.Generic;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.App.ReverseProxy;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;

using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

/// <summary>Tests for <see cref="DockYarpHostMatcherPolicy"/> candidate selection.</summary>
public sealed class DockYarpHostMatcherPolicyTests
{
    private const string HostPatternKey = "DockYarp.HostPattern";

    /// <summary>A candidate whose request host does not match the trailing pattern is invalidated.</summary>
    [Test]
    public async Task InvalidatesNonMatchingTrailingHost()
    {
        DockYarpHostMatcherPolicy policy = new();
        CandidateSet candidates = Candidates(PatternEndpoint("app.*"));

        await policy.ApplyAsync(Context("other.local"), candidates);

        candidates.IsValidCandidate(0).Should().BeFalse();
    }

    /// <summary>A candidate whose request host matches the trailing pattern stays valid.</summary>
    [Test]
    public async Task KeepsMatchingTrailingHost()
    {
        DockYarpHostMatcherPolicy policy = new();
        CandidateSet candidates = Candidates(PatternEndpoint("app.*"));

        await policy.ApplyAsync(Context("app.example.com"), candidates);

        candidates.IsValidCandidate(0).Should().BeTrue();
    }

    /// <summary>A regex-metadata candidate is invalidated for a non-matching host and kept for a matching one.</summary>
    [Test]
    public async Task RegexHostIsMatched()
    {
        DockYarpHostMatcherPolicy policy = new();
        CandidateSet matching = Candidates(PatternEndpoint(@"~^app-\d+\.local$"));
        CandidateSet notMatching = Candidates(PatternEndpoint(@"~^app-\d+\.local$"));

        await policy.ApplyAsync(Context("app-42.local"), matching);
        await policy.ApplyAsync(Context("nope.local"), notMatching);

        matching.IsValidCandidate(0).Should().BeTrue();
        notMatching.IsValidCandidate(0).Should().BeFalse();
    }

    /// <summary>The policy does not apply to endpoints without the host metadata.</summary>
    [Test]
    public void DoesNotApplyWithoutMetadata()
    {
        DockYarpHostMatcherPolicy policy = new();
        Endpoint endpoint = new(requestDelegate: null, EndpointMetadataCollection.Empty, "plain");

        policy.AppliesToEndpoints([endpoint]).Should().BeFalse();
    }

    private static Endpoint PatternEndpoint(string pattern)
    {
        RouteConfig config = new()
        {
            RouteId = "r",
            ClusterId = "c",
            Match = new RouteMatch { Path = "/{**catch-all}" },
            Metadata = new Dictionary<string, string>(System.StringComparer.Ordinal) { [HostPatternKey] = pattern },
        };
        RouteModel model = new(config, cluster: null, HttpTransformer.Empty);
        return new Endpoint(requestDelegate: null, new EndpointMetadataCollection(model), pattern);
    }

    private static CandidateSet Candidates(Endpoint endpoint) =>
        new([endpoint], [new RouteValueDictionary()], [0]);

    private static DefaultHttpContext Context(string host)
    {
        DefaultHttpContext context = new();
        context.Request.Host = new HostString(host);
        return context;
    }
}
