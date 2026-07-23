namespace DockYarp.App.StaticConfig;

using Microsoft.Extensions.Logging;

/// <summary>High-performance, source-generated log messages for static configuration.</summary>
internal static partial class StaticConfigLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Loaded {RouteCount} route(s) and {ClusterCount} cluster(s) from static configuration '{Path}'.")]
    public static partial void Loaded(ILogger logger, int routeCount, int clusterCount, string path);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Failed to load static configuration '{Path}': {Reason}")]
    public static partial void LoadFailed(ILogger logger, string path, string reason);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Static configuration merge [{Code}]: {Detail}")]
    public static partial void MergeDiagnostic(ILogger logger, string code, string detail);
}
