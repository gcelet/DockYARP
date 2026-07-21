namespace DockYarp.Core.Models;

/// <summary>Basic Auth credentials that protect a route.</summary>
/// <remarks>Stored on the routing model only; authentication is enforced by the security capability.</remarks>
public sealed record BasicAuthCredentials
{
    /// <summary>Gets the required username.</summary>
    public required string Username { get; init; }

    /// <summary>Gets the required password.</summary>
    public required string Password { get; init; }

    /// <summary>Gets the optional realm shown in the authentication challenge.</summary>
    public string? Realm { get; init; }
}
