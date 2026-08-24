namespace DockYarp.App.ErrorPages;

using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

/// <summary>Writes a configured custom error page as the body of a DockYarp-generated error response.</summary>
/// <remarks>Only rewrites responses that have not started and carry no body, so streamed backend responses are untouched.</remarks>
/// <param name="provider">The error-page provider.</param>
public sealed class ErrorPageMiddleware(ErrorPageProvider provider) : IMiddleware
{
    private const string HtmlContentType = "text/html; charset=utf-8";

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        await next(context).ConfigureAwait(false);

        HttpResponse response = context.Response;
        if (!response.HasStarted
            && response.StatusCode >= 400
            && response.ContentLength is null or 0
            && provider.TryGetPage(response.StatusCode, out string? html))
        {
            response.ContentType = HtmlContentType;
            await response.WriteAsync(html, context.RequestAborted).ConfigureAwait(false);
        }
    }
}
