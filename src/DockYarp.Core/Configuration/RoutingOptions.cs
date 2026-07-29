namespace DockYarp.Core.Configuration;

/// <summary>Options controlling default-host selection and the response for unmatched requests.</summary>
public sealed record RoutingOptions
{
    /// <summary>Gets the host whose route also serves requests matching no other host; <see langword="null"/> disables it.</summary>
    public string? DefaultHost { get; init; }

    /// <summary>Gets the HTTP status returned when a request matches no route and no default host.</summary>
    public int DefaultResponseStatusCode { get; init; } = 404;

    /// <summary>Gets an optional redirect target for unmatched requests; <see langword="null"/> returns the status only.</summary>
    /// <remarks>
    /// When set, the unmatched-request fallback redirects using <see cref="DefaultResponseStatusCode"/> as the
    /// status and this value (with <c>$scheme</c>/<c>$host</c>/<c>$request_uri</c> substitution, <c>$$</c> for a
    /// literal <c>$</c>) as the <c>Location</c>.
    /// </remarks>
    public string? DefaultResponseLocation { get; init; }
}
