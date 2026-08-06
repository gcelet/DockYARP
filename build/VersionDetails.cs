using System;

/// <summary>
/// The set of version strings stamped onto the produced assemblies, derived from GitVersion (or an explicit
/// version parameter / a static fallback when git history is unavailable, e.g. inside the Docker build stage).
/// </summary>
sealed class VersionDetails
{
    /// <summary>Stable <c>Major.Minor.Patch</c> used as the NuGet/package version prefix.</summary>
    public required string PackageVersionPrefix { get; init; }

    /// <summary>Pre-release tag (empty for a stable release) used as the package version suffix.</summary>
    public required string PackageVersionSuffix { get; init; }

    /// <summary>The full SemVer used for <c>-p:Version</c>.</summary>
    public required string Version { get; init; }

    /// <summary>The assembly version (<c>-p:AssemblyVersion</c>).</summary>
    public required string AssemblyVersion { get; init; }

    /// <summary>The file version (<c>-p:FileVersion</c>).</summary>
    public required string FileVersion { get; init; }

    /// <summary>The informational version (<c>-p:InformationalVersion</c>), including the commit id when available.</summary>
    public required string InformationalVersion { get; init; }

    /// <summary>Builds a deterministic fallback (<c>0.1.0</c>) used when GitVersion cannot resolve a version.</summary>
    /// <returns>A stable <see cref="VersionDetails"/> that lets the build proceed without git history.</returns>
    public static VersionDetails BuildDefaultFallbackVersion()
    {
        const string version = "0.1.0";
        return new VersionDetails
        {
            PackageVersionPrefix = version,
            PackageVersionSuffix = string.Empty,
            Version = version,
            AssemblyVersion = version,
            FileVersion = version,
            InformationalVersion = version,
        };
    }

    /// <summary>Builds version details from an explicit full version (the Docker build-arg case; no git history).</summary>
    /// <param name="fullVersion">The full version string computed on the host (e.g. <c>0.1.0-alpha.5</c>).</param>
    /// <returns>Version details whose prefix/suffix are split on the first <c>-</c>.</returns>
    public static VersionDetails FromExplicitVersion(string fullVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullVersion);

        int dash = fullVersion.IndexOf('-', StringComparison.Ordinal);
        string prefix = dash < 0 ? fullVersion : fullVersion[..dash];
        string suffix = dash < 0 ? string.Empty : fullVersion[(dash + 1)..];
        return new VersionDetails
        {
            PackageVersionPrefix = prefix,
            PackageVersionSuffix = suffix,
            Version = fullVersion,
            AssemblyVersion = prefix,
            FileVersion = prefix,
            InformationalVersion = fullVersion,
        };
    }
}
