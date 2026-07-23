namespace DockYarp.App.ErrorPages;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Abstractions;

/// <summary>Loads custom error pages (<c>{statusCode}.html</c>) from a directory, cached in memory.</summary>
public sealed class ErrorPageProvider
{
    private readonly Dictionary<int, string> pages;

    /// <summary>Loads the error pages from the configured directory, if any.</summary>
    /// <param name="options">Error-page options (directory).</param>
    /// <param name="fileSystem">Filesystem abstraction used to read the pages.</param>
    public ErrorPageProvider(ErrorPagesOptions options, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fileSystem);
        pages = Load(options, fileSystem);
    }

    /// <summary>Gets a value indicating whether any error page is configured.</summary>
    public bool HasPages => pages.Count > 0;

    /// <summary>Attempts to get the HTML page for a status code.</summary>
    /// <param name="statusCode">The response status code.</param>
    /// <param name="html">The page content when present.</param>
    /// <returns><see langword="true"/> when a page is configured for the status.</returns>
    public bool TryGetPage(int statusCode, [NotNullWhen(true)] out string? html) => pages.TryGetValue(statusCode, out html);

    private static Dictionary<int, string> Load(ErrorPagesOptions options, IFileSystem fileSystem)
    {
        Dictionary<int, string> result = [];
        if (options.Directory is not { Length: > 0 } directory || !fileSystem.Directory.Exists(directory))
        {
            return result;
        }

        foreach (string file in fileSystem.Directory.EnumerateFiles(directory, "*.html"))
        {
            string name = fileSystem.Path.GetFileNameWithoutExtension(file);
            if (int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int statusCode))
            {
                result[statusCode] = fileSystem.File.ReadAllText(file);
            }
        }

        return result;
    }
}
