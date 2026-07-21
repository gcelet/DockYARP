namespace DockYarp.AdminApi;

using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

/// <summary>Endpoint filter that requires a valid <c>X-Api-Key</c> header.</summary>
/// <param name="options">Admin API options carrying the expected key.</param>
public sealed class ApiKeyEndpointFilter(AdminApiOptions options) : IEndpointFilter
{
    private const string HeaderName = "X-Api-Key";

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        string provided = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (!IsValid(provided, options.ApiKey))
        {
            return Results.Unauthorized();
        }

        return await next(context).ConfigureAwait(false);
    }

    private static bool IsValid(string provided, string? expected)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected));
    }
}
