namespace DockYarp.E2E.Tests;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

using Microsoft.Extensions.Logging;

/// <summary>An <see cref="ILoggerProvider"/> that writes each log category to its own file, for e2e diagnostics.</summary>
/// <remarks>
/// Aspire's testing host redirects each resource's console output to the application's logging pipeline, so a
/// logging provider — not the dashboard's <c>ResourceLoggerService</c> — is how a test captures resource logs.
/// One <c>&lt;category&gt;.log</c> file is created lazily per category (the category is the resource name).
/// </remarks>
/// <param name="directory">Directory that receives the per-category log files.</param>
internal sealed class ResourceFileLoggerProvider(string directory) : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, ResourceFileLogger> loggers = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) =>
        loggers.GetOrAdd(categoryName, name => new ResourceFileLogger(Path.Combine(directory, FileNameFor(name))));

    private static string FileNameFor(string categoryName)
    {
        // Aspire logs a resource under the category "<AppHostAssembly>.Resources.<name>"; use the bare resource
        // name for those files, and keep the full category for framework logs.
        const string marker = ".Resources.";
        int index = categoryName.LastIndexOf(marker, StringComparison.Ordinal);
        string name = index >= 0 ? categoryName[(index + marker.Length)..] : categoryName;
        return Sanitize(name) + ".log";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (ResourceFileLogger logger in loggers.Values)
        {
            logger.Dispose();
        }

        loggers.Clear();
    }

    private static string Sanitize(string categoryName)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        Span<char> buffer = stackalloc char[categoryName.Length];
        for (int i = 0; i < categoryName.Length; i++)
        {
            char current = categoryName[i];
            buffer[i] = Array.IndexOf(invalid, current) >= 0 ? '_' : current;
        }

        return new string(buffer);
    }

    /// <summary>Appends formatted log messages for one category to a file, opened lazily on first write.</summary>
    /// <param name="filePath">The destination file.</param>
    private sealed class ResourceFileLogger(string filePath) : ILogger, IDisposable
    {
        private readonly Lock gate = new();
        private StreamWriter? writer;

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            string message = formatter(state, exception);
            lock (gate)
            {
                writer ??= new StreamWriter(filePath, append: false) { AutoFlush = true };
                writer.WriteLine(message);
                if (exception is not null)
                {
                    writer.WriteLine(exception);
                }
            }
        }

        /// <summary>Flushes and closes the file.</summary>
        public void Dispose()
        {
            lock (gate)
            {
                writer?.Dispose();
                writer = null;
            }
        }
    }
}
