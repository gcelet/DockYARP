namespace DockYarp.E2E.Tests;

using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>Shared helpers for end-to-end scenarios: request building, route polling, and echo/JSON parsing.</summary>
public abstract class E2ETestBase
{
    private const int DefaultPollSeconds = 40;
    private const int PollDelayMs = 500;

    /// <summary>Gets the HTTP client targeting DockYarp's proxy endpoint.</summary>
    protected static HttpClient Proxy => AspireAppHostFixture.Proxy;

    /// <summary>Builds a proxy request for a virtual host and path (the <c>Host</c> header drives routing).</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="host">The virtual host to route on.</param>
    /// <param name="path">The request path (relative to the proxy).</param>
    /// <returns>A fresh request message.</returns>
    protected static HttpRequestMessage Request(HttpMethod method, string host, string path)
    {
        HttpRequestMessage request = new(method, path);
        request.Headers.Host = host;
        return request;
    }

    /// <summary>Sends requests through the proxy client until <paramref name="accept"/> holds or it times out.</summary>
    /// <param name="factory">Creates a new request per attempt (a request cannot be resent).</param>
    /// <param name="accept">Predicate deciding whether a response is the awaited one.</param>
    /// <param name="timeoutSeconds">Overall polling budget.</param>
    /// <returns>The first accepted response, or the last one received when the budget elapses.</returns>
    protected static Task<HttpResponseMessage> PollAsync(
        Func<HttpRequestMessage> factory,
        Func<HttpResponseMessage, bool> accept,
        int timeoutSeconds = DefaultPollSeconds) =>
        PollAsync(Proxy, factory, accept, timeoutSeconds);

    /// <summary>Sends requests through <paramref name="client"/> until <paramref name="accept"/> holds or it times out.</summary>
    /// <param name="client">The HTTP client to send through (for example a TLS-aware HTTPS client).</param>
    /// <param name="factory">Creates a new request per attempt (a request cannot be resent).</param>
    /// <param name="accept">Predicate deciding whether a response is the awaited one.</param>
    /// <param name="timeoutSeconds">Overall polling budget.</param>
    /// <returns>The first accepted response, or the last one received when the budget elapses.</returns>
    protected static async Task<HttpResponseMessage> PollAsync(
        HttpClient client,
        Func<HttpRequestMessage> factory,
        Func<HttpResponseMessage, bool> accept,
        int timeoutSeconds)
    {
        long deadline = Environment.TickCount64 + (timeoutSeconds * 1000L);
        HttpResponseMessage? last = null;
        while (true)
        {
            using HttpRequestMessage request = factory();
            HttpResponseMessage response = await client.SendAsync(request);
            if (accept(response))
            {
                last?.Dispose();
                return response;
            }

            last?.Dispose();
            last = response;
            if (Environment.TickCount64 > deadline)
            {
                return response;
            }

            await Task.Delay(PollDelayMs);
        }
    }

    /// <summary>Polls a GET on a virtual host until it returns a success status (route is discovered and live).</summary>
    /// <param name="host">The virtual host to route on.</param>
    /// <param name="path">The request path.</param>
    /// <returns>The successful (or last) response.</returns>
    protected static Task<HttpResponseMessage> PollUntilSuccessAsync(string host, string path) =>
        PollAsync(() => Request(HttpMethod.Get, host, path), static response => response.IsSuccessStatusCode);

    /// <summary>Polls a request that returns JSON until the parsed body satisfies <paramref name="accept"/>.</summary>
    /// <param name="factory">Creates a new request per attempt.</param>
    /// <param name="accept">Predicate over the parsed body of a successful response.</param>
    /// <param name="timeoutSeconds">Overall polling budget.</param>
    /// <returns>The first accepted body, or the last successful body when the budget elapses.</returns>
    protected static async Task<JsonElement> PollJsonAsync(
        Func<HttpRequestMessage> factory,
        Func<JsonElement, bool> accept,
        int timeoutSeconds = DefaultPollSeconds)
    {
        long deadline = Environment.TickCount64 + (timeoutSeconds * 1000L);
        JsonElement last = default;
        while (true)
        {
            using HttpRequestMessage request = factory();
            using HttpResponseMessage response = await Proxy.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                last = await ReadJsonAsync(response);
                if (accept(last))
                {
                    return last;
                }
            }

            if (Environment.TickCount64 > deadline)
            {
                return last;
            }

            await Task.Delay(PollDelayMs);
        }
    }

    /// <summary>Reads a JSON response body and returns a detached clone of its root element.</summary>
    /// <param name="response">The response to read.</param>
    /// <returns>The parsed root element.</returns>
    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    /// <summary>Gets the <c>id</c> field of an echo-backend response, or <see langword="null"/> when absent.</summary>
    /// <param name="echo">The parsed echo response.</param>
    /// <returns>The backend identifier, or <see langword="null"/>.</returns>
    protected static string? EchoId(JsonElement echo) =>
        echo.TryGetProperty("id", out JsonElement id) && id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;

    /// <summary>Reports whether an echo-backend response saw a request header (case-insensitive).</summary>
    /// <param name="echo">The parsed echo response.</param>
    /// <param name="name">The header name to look for.</param>
    /// <returns><see langword="true"/> when the header was present on the proxied request.</returns>
    protected static bool HasHeader(JsonElement echo, string name) =>
        echo.TryGetProperty("headers", out JsonElement headers)
        && headers.EnumerateObject().Any(header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase));
}
