using System;

using DockYarp.AdminApi;
using DockYarp.App.ErrorPages;
using DockYarp.App.Observability;
using DockYarp.App.ReverseProxy;
using DockYarp.App.Routing;
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

// Do not advertise the server technology: disable Kestrel's built-in `Server` header (hardening).
// A configured value can still be emitted via Security:ServerHeader.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

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
// A trusted downstream proxy's X-Forwarded-* headers are appended to; otherwise they are replaced.
ForwardedTransformActions xForwardedAction =
    builder.Configuration.GetValue("Proxy:TrustDownstreamProxy", defaultValue: true)
        ? ForwardedTransformActions.Append
        : ForwardedTransformActions.Set;

// Routing options: default (catch-all) host and the response for genuinely unmatched requests.
RoutingOptions routingOptions = builder.Configuration.GetSection("Routing").Get<RoutingOptions>() ?? new();
builder.Services.AddSingleton(routingOptions);

builder.Services.AddDockYarpReverseProxy(xForwardedAction);

// Security options bound from the "Security" section (defaults preserved when unset).
SecurityHeadersOptions securityOptions =
    builder.Configuration.GetSection("Security").Get<SecurityHeadersOptions>() ?? new();
builder.Services.AddDockYarpSecurity(securityOptions);

// HTTPS redirection is gated on real certificate availability (store-backed).
builder.Services.AddSingleton<ICertificateAvailability, CertificateAvailabilityAdapter>();

// Data-plane endpoint ports, bound explicitly so the HTTPS endpoint can attach the per-connection TLS
// handshake callback (which bypasses ConfigureHttpsDefaults). Defaults 8080/8443 (non-root container).
ServerEndpointOptions serverEndpoints =
    builder.Configuration.GetSection("Server").Get<ServerEndpointOptions>() ?? new();
builder.Services.AddSingleton(serverEndpoints);

// TLS/ACME: certificate store, SNI, HTTP-01 challenge, and provisioning.
TlsOptions tlsOptions = builder.Configuration.GetSection("Tls").Get<TlsOptions>() ?? new();
builder.Services.AddDockYarpTls(tlsOptions);

// Data Protection is registered transitively (YARP uses it for session affinity). Persist its keys under the
// certificate volume so they survive container recreation, and encrypt them at rest when an operator supplies a
// certificate; otherwise the benign "unencrypted keys" warning is suppressed (no sensitive payload is protected).
builder.AddDockYarpDataProtection(
    builder.Configuration.GetSection("DataProtection").Get<DataProtectionOptions>() ?? new(),
    tlsOptions.CertificateDirectory);

// Admin API + observability (admin options, certificate inventory, metrics, access logging).
builder.Services.AddDockYarpObservability(builder.Configuration);

// Response compression (gzip/brotli) for compressible responses; on by default (Compression:Enabled).
builder.AddDockYarpResponseCompression();

var app = builder.Build();

// Create the meter eagerly so its gauges are present for the first scrape.
_ = app.Services.GetRequiredService<DockYarpMetrics>();

// Access logging wraps the whole pipeline so redirects and unmatched responses are logged too.
app.UseMiddleware<AccessLogMiddleware>();

// Compress compressible responses (before anything writes a body); no-op when disabled.
app.UseDockYarpResponseCompression();

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
app.MapFallback(context => DefaultResponseWriter.WriteAsync(context, routingOptions));

await app.RunAsync();

/// <summary>Entry point marker exposed for integration testing.</summary>
public partial class Program
{
    /// <summary>Prevents direct instantiation; exists only to expose the entry point for integration tests.</summary>
    protected Program()
    {
    }
}
