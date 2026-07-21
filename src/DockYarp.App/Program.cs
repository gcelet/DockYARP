using DockYarp.AdminApi;
using DockYarp.App.ReverseProxy;
using DockYarp.Core.Interfaces;
using DockYarp.Core.Stores;
using DockYarp.Security;
using DockYarp.Tls;

using OpenTelemetry.Metrics;

using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// The routing store is the single source of truth; YARP is driven from it via the bridge.
builder.Services.AddSingleton<IRouteConfigStore, RouteConfigStore>();
builder.Services.AddReverseProxy().LoadFromMemory([], []);
builder.Services.AddHostedService<YarpConfigBridge>();
builder.Services.AddDockYarpSecurity(new SecurityHeadersOptions());

// TLS/ACME: certificate store, SNI, HTTP-01 challenge, and provisioning.
TlsOptions tlsOptions = new() { ContactEmail = builder.Configuration["Tls:ContactEmail"] };
string? certificateDirectory = builder.Configuration["Tls:CertificateDirectory"];
if (!string.IsNullOrEmpty(certificateDirectory))
{
    tlsOptions.CertificateDirectory = certificateDirectory;
}

builder.Services.AddDockYarpTls(tlsOptions);

// Admin API + observability.
builder.Services.AddSingleton(new AdminApiOptions { ApiKey = builder.Configuration["AdminApi:ApiKey"] });
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

await app.RunAsync();

/// <summary>Entry point marker exposed for integration testing.</summary>
public partial class Program
{
    /// <summary>Prevents direct instantiation; exists only to expose the entry point for integration tests.</summary>
    protected Program()
    {
    }
}
