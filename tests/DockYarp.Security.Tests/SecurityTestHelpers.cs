namespace DockYarp.Security.Tests;

using DockYarp.Core.Models;
using DockYarp.Core.Stores;

using Microsoft.AspNetCore.Http;

/// <summary>Helpers for constructing HTTP contexts and stores in security middleware tests.</summary>
internal static class SecurityTestHelpers
{
    public static DefaultHttpContext Context(string scheme, string host, string path)
    {
        DefaultHttpContext context = new();
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        context.Request.Path = path;
        return context;
    }

    public static RouteConfigStore StoreWith(params RouteRule[] routes)
    {
        RouteConfigStore store = new();
        store.Apply([.. routes], []);
        return store;
    }
}
