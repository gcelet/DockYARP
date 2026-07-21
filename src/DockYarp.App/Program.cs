using DockYarp.App.ReverseProxy;
using DockYarp.Core.Interfaces;
using DockYarp.Core.Stores;
using DockYarp.Security;

using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// The routing store is the single source of truth; YARP is driven from it via the bridge.
builder.Services.AddSingleton<IRouteConfigStore, RouteConfigStore>();
builder.Services.AddReverseProxy().LoadFromMemory([], []);
builder.Services.AddHostedService<YarpConfigBridge>();
builder.Services.AddDockYarpSecurity(new SecurityHeadersOptions());

var app = builder.Build();

// Security (headers, HTTPS enforcement, Basic Auth) runs before the reverse proxy.
app.UseDockYarpSecurity();
app.MapReverseProxy();

await app.RunAsync();

/// <summary>Entry point marker exposed for integration testing.</summary>
public partial class Program
{
    /// <summary>Prevents direct instantiation; exists only to expose the entry point for integration tests.</summary>
    protected Program()
    {
    }
}
