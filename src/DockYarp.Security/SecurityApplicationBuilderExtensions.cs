namespace DockYarp.Security;

using System;

using Microsoft.AspNetCore.Builder;

/// <summary>Pipeline registration for the security middleware.</summary>
public static class SecurityApplicationBuilderExtensions
{
    /// <summary>Adds the security middlewares in order: headers, HTTPS redirect, client certificate, then Basic Auth.</summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same application builder for chaining.</returns>
    public static IApplicationBuilder UseDockYarpSecurity(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<HttpsRedirectionMiddleware>();
        app.UseMiddleware<ClientCertificateMiddleware>();
        app.UseMiddleware<BasicAuthMiddleware>();
        return app;
    }
}
