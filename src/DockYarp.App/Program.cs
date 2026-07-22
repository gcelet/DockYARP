using System;

using DockYarp.AdminApi;
using DockYarp.App.Observability;
using DockYarp.App.ReverseProxy;
using DockYarp.Core.Configuration;
using DockYarp.Core.Interfaces;
using DockYarp.Core.Stores;
using DockYarp.Docker;
using DockYarp.Docker.Discovery;
using DockYarp.Security;
using DockYarp.Tls;

using OpenTelemetry.Metrics;

using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// Graceful shutdown: drain in-flight requests and stop background workers within a bounded timeout.
int shutdownSeconds = builder.Configuration.GetValue("Host:ShutdownTimeoutSeconds", 30);
builder.Services.Configure<HostOptions>(host => host.ShutdownTimeout = TimeSpan.FromSeconds(shutdownSeconds));

// Docker discovery is opt-in (kept off in tests / local runs without a daemon).
if (builder.Configuration.GetValue("Docker:Enabled", defaultValue: false))
{
    DockerDiscoveryOptions dockerOptions = new();
    builder.Configuration.GetSection("Docker").Bind(dockerOptions);
    builder.Services.AddDockerDiscovery(dockerOptions);
    builder.Services.AddSingleton<IDiscoveryHealth, DiscoveryHealthAdapter>();
}
else
{
    builder.Services.AddSingleton<IDiscoveryHealth>(new DisabledDiscoveryHealth());
}

// The routing store is the single source of truth; YARP is driven from it via the bridge.
bool trustDownstreamProxy = builder.Configuration.GetValue("Proxy:TrustDownstreamProxy", defaultValue: true);
ForwardedTransformActions xForwardedAction =
    trustDownstreamProxy ? ForwardedTransformActions.Append : ForwardedTransformActions.Set;

// Routing options: default (catch-all) host and the response for genuinely unmatched requests.
RoutingOptions routingOptions = new();
builder.Configuration.GetSection("Routing").Bind(routingOptions);
builder.Services.AddSingleton(routingOptions);

builder.Services.AddSingleton<IRouteConfigStore, RouteConfigStore>();
builder.Services.AddReverseProxy()
    .LoadFromMemory([], [])
    .AddTransforms(context => ForwardedHeadersTransform.Apply(context, xForwardedAction));
builder.Services.AddHostedService<YarpConfigBridge>();

// Security options bound from the "Security" section (defaults preserved when unset).
SecurityHeadersOptions securityOptions = new();
builder.Configuration.GetSection("Security").Bind(securityOptions);
builder.Services.AddDockYarpSecurity(securityOptions);

// TLS/ACME: certificate store, SNI, HTTP-01 challenge, and provisioning.
TlsOptions tlsOptions = new();
builder.Configuration.GetSection("Tls").Bind(tlsOptions);
builder.Services.AddDockYarpTls(tlsOptions);

// Admin API + observability.
AdminApiOptions adminApiOptions = new();
builder.Configuration.GetSection("AdminApi").Bind(adminApiOptions);
builder.Services.AddSingleton(adminApiOptions);
builder.Services.AddSingleton<ICertificateInventory, CertificateInventoryAdapter>();
builder.Services.AddSingleton<DockYarpMetrics>();
builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics
    .AddMeter(DockYarpMetrics.MeterName)
    .AddPrometheusExporter());

var app = builder.Build();

// Create the meter eagerly so its gauges are present for the first scrape.
_ = app.Services.GetRequiredService<DockYarpMetrics>();

// ACME HTTP-01 challenge must be reachable over HTTP, before HTTPS enforcement.
app.UseDockYarpAcmeChallenge();

// Security (headers, HTTPS enforcement, Basic Auth) runs before the reverse proxy.
app.UseDockYarpSecurity();

// Explicit endpoints take routing precedence over YARP's catch-all.
app.MapAdminApi();
app.MapPrometheusScrapingEndpoint();
app.MapReverseProxy();

// Terminal fallback: requests matching no route (and no default host) get the configured default response.
app.MapFallback(() => Results.StatusCode(routingOptions.DefaultResponseStatusCode));

await app.RunAsync();

/// <summary>Entry point marker exposed for integration testing.</summary>
public partial class Program
{
    /// <summary>Prevents direct instantiation; exists only to expose the entry point for integration tests.</summary>
    protected Program()
    {
    }
}
