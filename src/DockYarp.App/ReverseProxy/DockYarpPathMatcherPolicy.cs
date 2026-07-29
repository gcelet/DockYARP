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

/// <summary>Matches regex <c>VIRTUAL_PATH</c> locations, which YARP's route-template path cannot express.</summary>
/// <remarks>
/// A regex-path route is emitted with a catch-all <c>Match.Path</c> and carries the regex body in
/// <c>RouteConfig.Metadata</c>. This endpoint-selector policy invalidates such candidates whose request path does
/// not match; prefix-path routes keep their specific template and remain more specific.
/// </remarks>
public sealed class DockYarpPathMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
{
    /// <summary>Route metadata key carrying a regex path body (the <c>VIRTUAL_PATH</c> without its <c>~</c>).</summary>
    internal const string PathRegexKey = "DockYarp.PathRegex";

    /// <inheritdoc />
    public override int Order => 1000;

    /// <inheritdoc />
    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints.Any(static endpoint => PathRegexFor(endpoint) is not null);
    }

    /// <inheritdoc />
    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(candidates);

        string path = httpContext.Request.Path.Value ?? "/";
        for (int i = 0; i < candidates.Count; i++)
        {
            if (!candidates.IsValidCandidate(i))
            {
                continue;
            }

            string? body = PathRegexFor(candidates[i].Endpoint);
            if (body is not null && !CompiledRegexCache.IsMatch(body, path))
            {
                candidates.SetValidity(i, false);
            }
        }

        return Task.CompletedTask;
    }

    private static string? PathRegexFor(Endpoint endpoint) =>
        endpoint.Metadata.GetMetadata<RouteModel>()?.Config.Metadata?.GetValueOrDefault(PathRegexKey);
}
