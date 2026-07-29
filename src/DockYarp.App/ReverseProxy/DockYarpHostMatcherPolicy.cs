namespace DockYarp.App.ReverseProxy;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using DockYarp.Core.Routing;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;

using Yarp.ReverseProxy.Model;

/// <summary>Matches host forms YARP cannot express natively (trailing wildcard today; regex added later).</summary>
/// <remarks>
/// Non-native host routes are emitted without a <c>Match.Hosts</c> constraint and carry the pattern in
/// <c>RouteConfig.Metadata</c>. This endpoint-selector policy invalidates such candidates whose request host does
/// not match, while native exact/leading-wildcard routes (which keep <c>Match.Hosts</c>) remain more specific and
/// win — matching nginx-proxy's exact &gt; wildcard precedence.
/// </remarks>
public sealed class DockYarpHostMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
{
    /// <summary>Route metadata key carrying a trailing-wildcard host pattern (<c>prefix.*</c>).</summary>
    internal const string HostTrailingKey = "DockYarp.HostTrailing";

    /// <inheritdoc />
    /// <remarks>Runs after the built-in host matcher policy so native host routes are resolved first.</remarks>
    public override int Order => 1000;

    /// <inheritdoc />
    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Endpoint-build time, not the request path: LINQ is fine here.
        return endpoints.Any(static endpoint => HostPatternFor(endpoint) is not null);
    }

    /// <inheritdoc />
    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(candidates);

        string host = httpContext.Request.Host.Host;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (!candidates.IsValidCandidate(i))
            {
                continue;
            }

            string? pattern = HostPatternFor(candidates[i].Endpoint);
            if (pattern is not null && !HostPattern.Parse(pattern).Matches(host))
            {
                candidates.SetValidity(i, false);
            }
        }

        return Task.CompletedTask;
    }

    private static string? HostPatternFor(Endpoint endpoint) =>
        endpoint.Metadata.GetMetadata<RouteModel>()?.Config.Metadata?.GetValueOrDefault(HostTrailingKey);
}
