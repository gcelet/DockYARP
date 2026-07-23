namespace DockYarp.IntegrationTests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.App.Observability;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>Tests for <see cref="AccessLogMiddleware"/>.</summary>
public sealed class AccessLogMiddlewareTests
{
    /// <summary>When enabled, one access-log entry is emitted and the pipeline continues.</summary>
    [Test]
    public async Task LogsWhenEnabled()
    {
        RecordingLogger logger = new();
        AccessLogMiddleware middleware = new(new AccessLogOptions { Enabled = true }, logger);
        DefaultHttpContext context = Context();
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
        logger.Entries.Should().ContainSingle().Which.Should().Be(LogLevel.Information);
    }

    /// <summary>When disabled, no entry is emitted but the pipeline continues.</summary>
    [Test]
    public async Task DoesNotLogWhenDisabled()
    {
        RecordingLogger logger = new();
        AccessLogMiddleware middleware = new(new AccessLogOptions { Enabled = false }, logger);
        DefaultHttpContext context = Context();
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
        logger.Entries.Should().BeEmpty();
    }

    /// <summary>A request under an excluded prefix (e.g. /metrics) is not logged.</summary>
    [Test]
    public async Task DoesNotLogExcludedPath()
    {
        RecordingLogger logger = new();
        AccessLogMiddleware middleware = new(new AccessLogOptions { Enabled = true }, logger);
        DefaultHttpContext context = Context("/metrics");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
        logger.Entries.Should().BeEmpty();
    }

    private static DefaultHttpContext Context(string path = "/orders")
    {
        DefaultHttpContext context = new();
        context.Request.Method = "GET";
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("app.local");
        context.Request.Path = path;
        return context;
    }

    private sealed class RecordingLogger : ILogger<AccessLogMiddleware>
    {
        public List<LogLevel> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add(logLevel);
    }
}
