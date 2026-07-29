namespace DockYarp.App.ReverseProxy;

using System;
using System.IO.Compression;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Registers response compression (gzip/brotli) for proxied responses, on by default.</summary>
public static class ResponseCompressionExtensions
{
    private const string EnabledKey = "Compression:Enabled";

    /// <summary>Adds response-compression services unless <c>Compression:Enabled</c> is set to <c>false</c>.</summary>
    /// <param name="builder">The web application builder.</param>
    public static void AddDockYarpResponseCompression(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!builder.Configuration.GetValue(EnabledKey, defaultValue: true))
        {
            return;
        }

        // Compress over HTTPS too (nginx-proxy parity); Fastest keeps proxy latency low. The middleware skips
        // responses that already carry a Content-Encoding, so upstream-compressed bodies are never re-compressed.
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });
        builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
    }

    /// <summary>Adds the response-compression middleware unless <c>Compression:Enabled</c> is set to <c>false</c>.</summary>
    /// <param name="app">The web application.</param>
    public static void UseDockYarpResponseCompression(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (app.Configuration.GetValue(EnabledKey, defaultValue: true))
        {
            app.UseResponseCompression();
        }
    }
}
