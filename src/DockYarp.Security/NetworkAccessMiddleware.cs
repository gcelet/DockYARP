namespace DockYarp.Security;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading.Tasks;

using DockYarp.Core.Models;

using Microsoft.AspNetCore.Http;

/// <summary>Restricts internal-only routes to clients within the configured internal network ranges.</summary>
/// <remarks>
/// Enforces <c>NETWORK_ACCESS=internal</c>: a request whose route is internal-only is answered with 403 unless
/// the client's connection IP is within an internal range. The IP is the direct connection address
/// (<see cref="ConnectionInfo.RemoteIpAddress"/>); an address that cannot be determined is treated as external.
/// </remarks>
public sealed class NetworkAccessMiddleware : IMiddleware
{
    private readonly RouteLookup routes;
    private readonly IPNetwork[] internalRanges;

    /// <summary>Initializes the middleware, parsing the configured internal ranges once.</summary>
    /// <param name="routes">Route lookup used to find the request's route.</param>
    /// <param name="options">Security options carrying the internal CIDR ranges.</param>
    public NetworkAccessMiddleware(RouteLookup routes, SecurityHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.routes = routes;

        List<IPNetwork> parsed = [];
        foreach (string range in options.InternalRanges)
        {
            if (IPNetwork.TryParse(range, out IPNetwork network))
            {
                parsed.Add(network);
            }
        }

        internalRanges = [.. parsed];
    }

    /// <inheritdoc />
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (routes.TryGetRoute(context, out RouteRule? route)
            && route.InternalOnly
            && !IsInternal(context.Connection.RemoteIpAddress))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        return next(context);
    }

    [SuppressMessage(
        "SonarAnalyzer",
        "S3267:Loops should be simplified with LINQ",
        Justification = "Request path: an explicit loop avoids a per-request closure allocation (low-allocation guideline).")]
    private bool IsInternal(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        // Kestrel dual-stack sockets surface IPv4 peers as IPv4-mapped IPv6 (::ffff:a.b.c.d); normalize so they
        // match IPv4 ranges.
        IPAddress candidate = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        foreach (IPNetwork network in internalRanges)
        {
            if (network.Contains(candidate))
            {
                return true;
            }
        }

        return false;
    }
}
