namespace DockYarp.Security;

using System.Globalization;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

/// <summary>Adds baseline security headers (and HSTS on HTTPS) to every response.</summary>
/// <param name="options">Header configuration.</param>
public sealed class SecurityHeadersMiddleware(SecurityHeadersOptions options) : IMiddleware
{
    /// <inheritdoc />
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        IHeaderDictionary headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = options.FrameOptions;
        headers["Referrer-Policy"] = options.ReferrerPolicy;

        if (options.EnableHsts && context.Request.IsHttps)
        {
            headers["Strict-Transport-Security"] = BuildHsts();
        }

        return next(context);
    }

    private string BuildHsts()
    {
        long seconds = (long)options.HstsMaxAge.TotalSeconds;
        string value = string.Create(CultureInfo.InvariantCulture, $"max-age={seconds}");
        return options.HstsIncludeSubDomains ? $"{value}; includeSubDomains" : value;
    }
}
