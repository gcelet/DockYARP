namespace DockYarp.Core.Models;

/// <summary>Controls HTTP↔HTTPS behavior for a host (nginx-proxy <c>HTTPS_METHOD</c>).</summary>
public enum HttpsMethod
{
    /// <summary>Serve HTTPS and redirect HTTP to HTTPS (the default).</summary>
    Redirect,

    /// <summary>Serve both HTTP and HTTPS without redirecting.</summary>
    NoRedirect,

    /// <summary>Serve HTTPS only (HTTP requests are redirected to HTTPS).</summary>
    NoHttp,

    /// <summary>Serve HTTP only (never redirect to HTTPS).</summary>
    NoHttps,
}
