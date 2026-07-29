namespace DockYarp.Core.Routing;

/// <summary>The kind of a <c>VIRTUAL_HOST</c> pattern, in descending match precedence.</summary>
public enum HostPatternKind
{
    /// <summary>An exact host (highest precedence), for example <c>app.local</c>.</summary>
    Exact,

    /// <summary>A leading wildcard <c>*.suffix</c> matching a subdomain of any depth.</summary>
    LeadingWildcard,

    /// <summary>A trailing wildcard <c>prefix.*</c> matching any host beginning with <c>prefix.</c>.</summary>
    TrailingWildcard,

    /// <summary>A <c>~</c>-prefixed regular expression (lowest precedence).</summary>
    Regex,
}
