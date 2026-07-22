namespace DockYarp.Core.Configuration;

/// <summary>Options controlling default-host selection and the response for unmatched requests.</summary>
public sealed record RoutingOptions
{
    /// <summary>Gets the host whose route also serves requests matching no other host; <see langword="null"/> disables it.</summary>
    public string? DefaultHost { get; init; }

    /// <summary>Gets the HTTP status returned when a request matches no route and no default host.</summary>
    public int DefaultResponseStatusCode { get; init; } = 404;
}
