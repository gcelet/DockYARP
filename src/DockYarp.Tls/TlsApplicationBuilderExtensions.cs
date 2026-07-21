namespace DockYarp.Tls;

using System;

using Microsoft.AspNetCore.Builder;

/// <summary>Pipeline registration for the ACME HTTP-01 challenge middleware.</summary>
public static class TlsApplicationBuilderExtensions
{
    /// <summary>Adds the ACME HTTP-01 challenge middleware (must run before HTTPS enforcement).</summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same application builder for chaining.</returns>
    public static IApplicationBuilder UseDockYarpAcmeChallenge(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseMiddleware<Http01ChallengeMiddleware>();
        return app;
    }
}
