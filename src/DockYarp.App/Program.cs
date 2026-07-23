using System;

using DockYarp.AdminApi;
using DockYarp.App.ErrorPages;
using DockYarp.App.Observability;
using DockYarp.App.ReverseProxy;
using DockYarp.App.Security;
using DockYarp.App.StaticConfig;
using DockYarp.Core.Configuration;
using DockYarp.Docker;
using DockYarp.Docker.Discovery;
using DockYarp.Security;
using DockYarp.Tls;

using OpenTelemetry.Metrics;

using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// Graceful shutdown: drain in-flight requests and stop background workers within a bounded timeout.
int shutdownSeconds = builder.Configuration.GetValue("Host:ShutdownTimeoutSeconds", 30);
builder.Services.Configure<HostOptions>(host => host.ShutdownTimeout = TimeSpan.FromSeconds(shutdownSeconds));

// Static (file-based) configuration source, merged with discovery (static wins).
builder.Services.AddDockYarpStaticConfig(builder.Configuration);

// Custom error pages for DockYarp-generated error responses.
builder.Services.AddDockYarpErrorPages(builder.Configuration);

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
    // No discovery: apply the static configuration to the store at startup.
    builder.Services.AddSingleton<IDiscoveryHealth>(new DisabledDiscoveryHealth());
    builder.Services.AddHostedService<StaticConfigService>();
}

// The routing store is the single source of truth; YARP is driven from it via the bridge.
bool trustDownstreamProxy = builder.Configuration.GetValue("Proxy:TrustDownstreamProxy", defaultValue: true);
ForwardedTransformActions xForwardedAction =
    trustDownstreamProxy ? ForwardedTransformActions.Append : ForwardedTransformActions.Set;

// Routing options: default (catch-all) host and the response for genuinely unmatched requests.
RoutingOptions routingOptions = new();
builder.Configuration.GetSection("Routing").Bind(routingOptions);
builder.Services.AddSingleton(routingOptions);

builder.Services.AddDockYarpReverseProxy(xForwardedAction);

// Security options bound from the "Security" section (defaults preserved when unset).
SecurityHeadersOptions securityOptions = new();
builder.Configuration.GetSection("Security").Bind(securityOptions);
builder.Services.AddDockYarpSecurity(securityOptions);

// HTTPS redirection is gated on real certificate availability (store-backed).
builder.Services.AddSingleton<ICertificateAvailability, CertificateAvailabilityAdapter>();

// TLS/ACME: certificate store, SNI, HTTP-01 challenge, and provisioning.
TlsOptions tlsOptions = new();
builder.Configuration.GetSection("Tls").Bind(tlsOptions);
builder.Services.AddDockYarpTls(tlsOptions);

// Admin API + observability (admin options, certificate inventory, metrics, access logging).
builder.Services.AddDockYarpObservability(builder.Configuration);

var app = builder.Build();

// Create the meter eagerly so its gauges are present for the first scrape.
_ = app.Services.GetRequiredService<DockYarpMetrics>();

// Access logging wraps the whole pipeline so redirects and unmatched responses are logged too.
app.UseMiddleware<AccessLogMiddleware>();

// Overlay a configured error page onto DockYarp-generated error responses.
app.UseMiddleware<ErrorPageMiddleware>();

// ACME HTTP-01 challenge must be reachable over HTTP, before HTTPS enforcement.
app.UseDockYarpAcmeChallenge();

// Security (headers, HTTPS enforcement, Basic Auth) runs before the reverse proxy.
app.UseDockYarpSecurity();

// Apply per-route request limits before proxying.
app.UseMiddleware<RequestBodySizeMiddleware>();

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
