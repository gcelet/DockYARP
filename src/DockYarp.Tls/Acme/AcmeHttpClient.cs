namespace DockYarp.Tls.Acme;

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Low-level ACME v2 (RFC 8555) wire operations: directory discovery, replay-nonce tracking,
/// JWS(ES256)-signed requests (delegating to <see cref="AcmeJws"/>), and the one bounded retry RFC 8555
/// §6.7 documents for a stale nonce. One instance is scoped to a single account key, matching Certes' own
/// per-order-request <c>AcmeContext</c> lifetime — a fresh account is created per certificate request.</summary>
/// <param name="http">The HTTP client to send ACME requests through.</param>
/// <param name="directoryUri">The ACME server's directory URL.</param>
/// <param name="accountKey">The account's ES256 key — signs every request from <see cref="CreateAccountAsync"/> on.</param>
internal sealed class AcmeHttpClient(HttpClient http, Uri directoryUri, ECDsa accountKey)
{
    private const string JoseContentType = "application/jose+json";
    private const string BadNonceProblemType = "urn:ietf:params:acme:error:badNonce";

    private AcmeDirectory? directory;
    private string? nonce;
    private string? kid;

    /// <summary>Creates the ACME account and returns its key ID (the <c>Location</c> the CA assigned it).</summary>
    /// <param name="request">The account request — contact and terms-of-service agreement.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task CreateAccountAsync(AcmeNewAccountRequest request, CancellationToken cancellationToken)
    {
        AcmeDirectory acmeDirectory = await DirectoryAsync(cancellationToken).ConfigureAwait(false);
        using HttpResponseMessage response = await SendSignedAsync(
            acmeDirectory.NewAccount, request, AcmeJsonContext.Default.AcmeNewAccountRequest, cancellationToken)
            .ConfigureAwait(false);
        kid = response.Headers.Location?.ToString()
            ?? throw new InvalidOperationException("The ACME server did not return an account URL.");
    }

    /// <summary>Creates a single-host order and returns it.</summary>
    /// <param name="host">The host (or <c>*.host</c> for a wildcard DNS-01 order) to request a certificate for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The order's own URL (from the response's <c>Location</c> header, needed to poll it later
    /// after finalizing) alongside the parsed order.</returns>
    public async Task<AcmeOrderCreated> CreateOrderAsync(string host, CancellationToken cancellationToken)
    {
        AcmeDirectory acmeDirectory = await DirectoryAsync(cancellationToken).ConfigureAwait(false);
        AcmeNewOrderRequest request = new() { Identifiers = [new AcmeIdentifier { Type = "dns", Value = host }] };
        using HttpResponseMessage response = await SendSignedAsync(
            acmeDirectory.NewOrder, request, AcmeJsonContext.Default.AcmeNewOrderRequest, cancellationToken)
            .ConfigureAwait(false);
        string orderUrl = response.Headers.Location?.ToString()
            ?? throw new InvalidOperationException("The ACME server did not return an order URL.");
        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        AcmeOrder order = JsonSerializer.Deserialize(body, AcmeJsonContext.Default.AcmeOrder)
            ?? throw new InvalidOperationException("The ACME server returned an empty order.");
        return new AcmeOrderCreated(orderUrl, order);
    }

    /// <summary>Fetches an authorization resource (also used to poll its status after triggering validation).</summary>
    /// <param name="authorizationUrl">The authorization URL, from the order's own <c>authorizations</c> list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<AcmeAuthorization> GetAuthorizationAsync(string authorizationUrl, CancellationToken cancellationToken) =>
        SendSignedForJsonAsync(
            authorizationUrl, (object?)null, AcmeJsonContext.Default.Object,
            AcmeJsonContext.Default.AcmeAuthorization, cancellationToken);

    /// <summary>Re-fetches an order resource — used to poll its status after finalizing (RFC 8555 §7.4).</summary>
    /// <param name="orderUrl">The order's own URL, from <see cref="CreateOrderAsync"/>'s result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<AcmeOrder> GetOrderAsync(string orderUrl, CancellationToken cancellationToken) =>
        SendSignedForJsonAsync(
            orderUrl, (object?)null, AcmeJsonContext.Default.Object,
            AcmeJsonContext.Default.AcmeOrder, cancellationToken);

    /// <summary>Triggers CA-side validation of a challenge (POST an empty object to its own URL).</summary>
    /// <param name="challengeUrl">The challenge's own <c>url</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task TriggerChallengeAsync(string challengeUrl, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendSignedAsync(
            challengeUrl, new object(), AcmeJsonContext.Default.Object, cancellationToken).ConfigureAwait(false);
        response.Dispose();
    }

    /// <summary>Submits the CSR to finalize the order once every authorization is valid.</summary>
    /// <param name="finalizeUrl">The order's own <c>finalize</c> URL.</param>
    /// <param name="csrDer">The DER-encoded PKCS#10 CSR.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<AcmeOrder> FinalizeOrderAsync(string finalizeUrl, byte[] csrDer, CancellationToken cancellationToken)
    {
        AcmeFinalizeRequest request = new() { Csr = System.Buffers.Text.Base64Url.EncodeToString(csrDer) };
        return SendSignedForJsonAsync(
            finalizeUrl, request, AcmeJsonContext.Default.AcmeFinalizeRequest,
            AcmeJsonContext.Default.AcmeOrder, cancellationToken);
    }

    /// <summary>Downloads the issued certificate chain as concatenated PEM (leaf + intermediates).</summary>
    /// <param name="certificateUrl">The order's own <c>certificate</c> URL, once its status is valid.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<string> DownloadCertificateChainAsync(string certificateUrl, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendSignedAsync(
            certificateUrl, (object?)null, AcmeJsonContext.Default.Object, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<AcmeDirectory> DirectoryAsync(CancellationToken cancellationToken)
    {
        if (directory is not null)
        {
            return directory;
        }

        using HttpResponseMessage response = await http.GetAsync(directoryUri, cancellationToken).ConfigureAwait(false);
        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        directory = JsonSerializer.Deserialize(body, AcmeJsonContext.Default.AcmeDirectory)
            ?? throw new InvalidOperationException("The ACME directory response was empty.");
        return directory;
    }

    private async Task<string> FreshNonceAsync(CancellationToken cancellationToken)
    {
        if (nonce is { } current)
        {
            nonce = null;
            return current;
        }

        AcmeDirectory acmeDirectory = await DirectoryAsync(cancellationToken).ConfigureAwait(false);
        using HttpRequestMessage request = new(HttpMethod.Head, acmeDirectory.NewNonce);
        using HttpResponseMessage response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return ReplayNonce(response.Headers)
            ?? throw new InvalidOperationException("The ACME server did not return a replay-nonce.");
    }

    private async Task<TResponse> SendSignedForJsonAsync<TRequest, TResponse>(
        string url,
        TRequest? payload,
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TResponse> responseTypeInfo,
        CancellationToken cancellationToken)
        where TRequest : class
        where TResponse : class
    {
        using HttpResponseMessage response =
            await SendSignedAsync(url, payload, requestTypeInfo, cancellationToken).ConfigureAwait(false);
        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, responseTypeInfo)
            ?? throw new InvalidOperationException($"The ACME server returned an empty response for {url}.");
    }

    private async Task<HttpResponseMessage> SendSignedAsync<TRequest>(
        string url, TRequest? payload, JsonTypeInfo<TRequest> typeInfo, CancellationToken cancellationToken)
        where TRequest : class
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            string currentNonce = await FreshNonceAsync(cancellationToken).ConfigureAwait(false);
            AcmeJws.JwsRequestContext context = new(accountKey, url, currentNonce, kid);
            string body = AcmeJws.Sign(context, payload, typeInfo);

            using StringContent content = new(body, Encoding.UTF8, JoseContentType);
            content.Headers.ContentType!.CharSet = null; // RFC 8555 §6.2: exactly "application/jose+json", no charset param.
            HttpResponseMessage response = await http.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            nonce = ReplayNonce(response.Headers) ?? nonce;

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            byte[] problemBody = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            AcmeProblemDetails? problem = JsonSerializer.Deserialize(problemBody, AcmeJsonContext.Default.AcmeProblemDetails);
            response.Dispose();

            if (attempt == 0 && problem?.Type == BadNonceProblemType)
            {
                continue;
            }

            throw new InvalidOperationException(
                $"ACME request to {url} failed: {problem?.Detail ?? problem?.Type ?? "unknown error"}");
        }

        throw new InvalidOperationException($"ACME request to {url} exhausted its badNonce retry.");
    }

    private static string? ReplayNonce(HttpResponseHeaders headers) =>
        headers.TryGetValues("Replay-Nonce", out System.Collections.Generic.IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : null;
}
