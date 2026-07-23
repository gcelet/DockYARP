namespace DockYarp.App.Observability;

using Microsoft.Extensions.Logging;

/// <summary>High-performance, source-generated access-log message.</summary>
internal static partial class AccessLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "{Method} {Scheme}://{Host}{Path} responded {StatusCode} in {ElapsedMs} ms")]
    public static partial void Request(
        ILogger logger,
        string method,
        string scheme,
        string host,
        string path,
        int statusCode,
        double elapsedMs);
}
