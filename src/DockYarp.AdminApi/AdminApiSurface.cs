namespace DockYarp.AdminApi;

/// <summary>What the admin surface exposes.</summary>
public enum AdminApiSurface
{
    /// <summary>Nothing is served: <c>/api/*</c>, <c>/metrics</c>, and <c>/dashboard</c> are not mapped, and
    /// fall through to normal reverse proxying like any other path.</summary>
    Disabled,

    /// <summary>The JSON admin API and <c>/metrics</c> are served (behind <see cref="AdminApiOptions.ApiKey"/>);
    /// <c>/dashboard</c> is not served.</summary>
    Api,

    /// <summary>The JSON admin API, <c>/metrics</c>, and the read-only <c>/dashboard</c> are all served.</summary>
    ApiAndDashboard,
}
