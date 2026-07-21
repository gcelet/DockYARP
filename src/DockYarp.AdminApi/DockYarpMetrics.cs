namespace DockYarp.AdminApi;

using System;
using System.Diagnostics.Metrics;
using System.Linq;

using DockYarp.Core.Interfaces;
using DockYarp.Core.Models;

/// <summary>Owns the OpenTelemetry meter and gauges reporting DockYarp's live state.</summary>
/// <remarks>Instantiated once at startup so the gauges exist for the first scrape.</remarks>
public sealed class DockYarpMetrics : IDisposable
{
    /// <summary>The meter name registered with OpenTelemetry.</summary>
    public const string MeterName = "DockYarp";

    private readonly Meter meter;

    /// <summary>Creates the meter and observable gauges backed by the routing store.</summary>
    /// <param name="store">The routing store the gauges read from.</param>
    public DockYarpMetrics(IRouteConfigStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        meter = new Meter(MeterName);
        meter.CreateObservableGauge("dockyarp.routes", () => store.Current.Routes.Length);
        meter.CreateObservableGauge("dockyarp.clusters", () => store.Current.Clusters.Length);
        meter.CreateObservableGauge("dockyarp.endpoints", () => CountEndpoints(store.Current));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        meter.Dispose();
        GC.SuppressFinalize(this);
    }

    private static int CountEndpoints(RouteConfigSnapshot snapshot) =>
        snapshot.Clusters.Sum(cluster => cluster.Endpoints.Length);
}
