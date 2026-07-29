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

/// <summary>Tests for <see cref="DockYarpPathMatcherPolicy"/> candidate selection.</summary>
public sealed class DockYarpPathMatcherPolicyTests
{
    private const string PathRegexKey = "DockYarp.PathRegex";

    /// <summary>A candidate whose request path does not match the regex is invalidated.</summary>
    [Test]
    public async Task InvalidatesNonMatchingPath()
    {
        DockYarpPathMatcherPolicy policy = new();
        CandidateSet candidates = Candidates(RegexPathEndpoint("^/(app1|alt1)/"));

        await policy.ApplyAsync(Context("/other"), candidates);

        candidates.IsValidCandidate(0).Should().BeFalse();
    }

    /// <summary>A candidate whose request path matches the regex stays valid.</summary>
    [Test]
    public async Task KeepsMatchingPath()
    {
        DockYarpPathMatcherPolicy policy = new();
        CandidateSet candidates = Candidates(RegexPathEndpoint("^/(app1|alt1)/"));

        await policy.ApplyAsync(Context("/app1/x"), candidates);

        candidates.IsValidCandidate(0).Should().BeTrue();
    }

    /// <summary>The policy does not apply to endpoints without the path metadata.</summary>
    [Test]
    public void DoesNotApplyWithoutMetadata()
    {
        DockYarpPathMatcherPolicy policy = new();
        Endpoint endpoint = new(requestDelegate: null, EndpointMetadataCollection.Empty, "plain");

        policy.AppliesToEndpoints([endpoint]).Should().BeFalse();
    }

    private static Endpoint RegexPathEndpoint(string body)
    {
        RouteConfig config = new()
        {
            RouteId = "r",
            ClusterId = "c",
            Match = new RouteMatch { Path = "/{**catch-all}" },
            Metadata = new Dictionary<string, string>(System.StringComparer.Ordinal) { [PathRegexKey] = body },
        };
        RouteModel model = new(config, cluster: null, HttpTransformer.Empty);
        return new Endpoint(requestDelegate: null, new EndpointMetadataCollection(model), body);
    }

    private static CandidateSet Candidates(Endpoint endpoint) =>
        new([endpoint], [new RouteValueDictionary()], [0]);

    private static DefaultHttpContext Context(string path)
    {
        DefaultHttpContext context = new();
        context.Request.Path = path;
        return context;
    }
}
