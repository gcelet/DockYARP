namespace DockYarp.E2E.AppHost;

using System.Collections.Generic;

/// <summary>Declarative description of a labeled backend container the AppHost adds to the system.</summary>
/// <remarks>
/// Kept purely as data so <see cref="BackendCatalog"/> can enumerate the backends and the AppHost entry point
/// stays a short, data-driven loop.
/// </remarks>
internal sealed record BackendSpec
{
    /// <summary>Gets the Aspire resource name (also the container name).</summary>
    public required string Name { get; init; }

    /// <summary>Gets the container image reference.</summary>
    public required string Image { get; init; }

    /// <summary>Gets the image tag.</summary>
    public string Tag { get; init; } = "latest";

    /// <summary>Gets the DockYarp discovery labels, each formatted as <c>KEY=VALUE</c>.</summary>
    public required IReadOnlyList<string> Labels { get; init; }

    /// <summary>Gets the container environment variables (for example the echo backend listen URLs).</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } =
        new Dictionary<string, string>(System.StringComparer.Ordinal);

    /// <summary>Gets extra <c>docker run</c> arguments (for example a deliberately failing health check).</summary>
    public IReadOnlyList<string> ExtraRuntimeArgs { get; init; } = [];

    /// <summary>Gets a value indicating whether the harness should wait for this backend to be running.</summary>
    /// <remarks>The deliberately unhealthy backend sets this to <see langword="false"/>.</remarks>
    public bool WaitForRunning { get; init; } = true;

    /// <summary>Builds the extra <c>docker run</c> arguments: each label as <c>--label KEY=VALUE</c>, then extras.</summary>
    /// <returns>The argument array passed to the container runtime.</returns>
    public string[] ToRuntimeArgs()
    {
        List<string> args = new((Labels.Count * 2) + ExtraRuntimeArgs.Count);
        foreach (string label in Labels)
        {
            args.Add("--label");
            args.Add(label);
        }

        args.AddRange(ExtraRuntimeArgs);
        return [.. args];
    }
}
