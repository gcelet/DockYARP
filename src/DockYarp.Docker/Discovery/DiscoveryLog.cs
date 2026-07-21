namespace DockYarp.Docker.Discovery;

using System;

using Microsoft.Extensions.Logging;

/// <summary>High-performance, source-generated log messages for Docker discovery.</summary>
internal static partial class DiscoveryLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Reconciled {RouteCount} route(s) and {ClusterCount} cluster(s) from {ContainerCount} container(s).")]
    public static partial void Reconciled(ILogger logger, int routeCount, int clusterCount, int containerCount);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Skipped container: {Reason}")]
    public static partial void ContainerSkipped(ILogger logger, string reason);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Configuration merge [{Code}]: {Detail}")]
    public static partial void MergeDiagnostic(ILogger logger, string code, string detail);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Connected to Docker; watching container events.")]
    public static partial void Connected(ILogger logger);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "Docker watch ended or failed (attempt {Attempt}); reconnecting in {Delay}.")]
    public static partial void Reconnecting(ILogger logger, int attempt, TimeSpan delay, Exception? exception);
}
