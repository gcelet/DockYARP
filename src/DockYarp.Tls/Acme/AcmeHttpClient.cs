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
/// JWS(ES256)-signed requests (delegating to <see cref="AcmeJws"/>), and one bounded retry — either RFC 8555
/// §6.7's stale-nonce case, or a <c>rateLimited</c> error carrying a <c>Retry-After</c> header (RFC 8555 §6.6,
/// waited out before retrying). One instance is scoped to a single account key; a fresh instance is
/// created per certificate request, but the key it's constructed with is persisted and reused across
/// requests via <see cref="AcmeAccountKeyStore"/> — <see cref="CreateAccountAsync"/> relies on
/// <c>newAccount</c>'s own idempotency to resolve repeated calls to the same account rather than a new one
/// each time.</summary>
/// <param name="http">The HTTP client to send ACME requests through.</param>
/// <param name="directoryUri">The ACME server's directory URL.</param>
/// <param name="accountKey">The account's ES256 key — signs every request from <see cref="CreateAccountAsync"/> on.</param>
internal sealed class AcmeHttpClient(HttpClient http, Uri directoryUri, ECDsa accountKey)
{
    private const string JoseContentType = "application/jose+json";
    private const string BadNonceProblemType = "urn:ietf:params:acme:error:badNonce";
    private const string RateLimitedProblemType = "urn:ietf:params:acme:error:rateLimited";

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

    /// <summary>Fetches an authorization resource (also used to poll its status after triggering validation),
    /// alongside any <c>Retry-After</c> the CA suggested for the next poll.</summary>
    /// <param name="authorizationUrl">The authorization URL, from the order's own <c>authorizations</c> list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<AcmePollResult<AcmeAuthorization>> GetAuthorizationAsync(string authorizationUrl, CancellationToken cancellationToken) =>
        SendSignedForPollAsync(authorizationUrl, AcmeJsonContext.Default.AcmeAuthorization, cancellationToken);

    /// <summary>Re-fetches an order resource — used to poll its status after finalizing (RFC 8555 §7.4) —
    /// alongside any <c>Retry-After</c> the CA suggested for the next poll.</summary>
    /// <param name="orderUrl">The order's own URL, from <see cref="CreateOrderAsync"/>'s result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<AcmePollResult<AcmeOrder>> GetOrderAsync(string orderUrl, CancellationToken cancellationToken) =>
        SendSignedForPollAsync(orderUrl, AcmeJsonContext.Default.AcmeOrder, cancellationToken);

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

    /// <summary>Revokes a certificate (RFC 8555 §7.6) — no revocation reason is sent (the field is optional
    /// and no operator-facing reason selection exists).</summary>
    /// <param name="certificateDer">The DER-encoded certificate to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">The CA's directory does not advertise a <c>revokeCert</c>
    /// URL (RFC 8555 marks it optional — not every CA supports revocation).</exception>
    public async Task RevokeCertificateAsync(byte[] certificateDer, CancellationToken cancellationToken)
    {
        AcmeDirectory acmeDirectory = await DirectoryAsync(cancellationToken).ConfigureAwait(false);
        string revokeUrl = acmeDirectory.RevokeCert
            ?? throw new InvalidOperationException("The ACME server's directory does not support certificate revocation.");

        AcmeRevokeCertificateRequest request = new()
        {
            Certificate = System.Buffers.Text.Base64Url.EncodeToString(certificateDer),
        };
        using HttpResponseMessage response = await SendSignedAsync(
            revokeUrl, request, AcmeJsonContext.Default.AcmeRevokeCertificateRequest, cancellationToken)
            .ConfigureAwait(false);
        response.Dispose();
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

    // Same shape as SendSignedForJsonAsync (POST-as-GET, RFC 8555 §6.3), but also surfaces the response's
    // Retry-After — needed only for the two status-polling call sites (GetAuthorizationAsync/GetOrderAsync),
    // not for every SendSignedForJsonAsync caller (e.g. FinalizeOrderAsync).
    private async Task<AcmePollResult<TResponse>> SendSignedForPollAsync<TResponse>(
        string url, JsonTypeInfo<TResponse> responseTypeInfo, CancellationToken cancellationToken)
        where TResponse : class
    {
        using HttpResponseMessage response = await SendSignedAsync(
            url, (object?)null, AcmeJsonContext.Default.Object, cancellationToken).ConfigureAwait(false);
        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        TResponse resource = JsonSerializer.Deserialize(body, responseTypeInfo)
            ?? throw new InvalidOperationException($"The ACME server returned an empty response for {url}.");
        return new AcmePollResult<TResponse>(resource, AcmeRetryAfter.Capped(response.Headers));
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

            // Captured before Dispose(): only a rateLimited error with an actual Retry-After to honor
            // qualifies for the wait-then-retry path below — a rateLimited error with no Retry-After still
            // fails immediately, same as any other error type (no retry is invented without a CA-supplied
            // duration to honor).
            TimeSpan? rateLimitWait = problem?.Type == RateLimitedProblemType ? AcmeRetryAfter.Capped(response.Headers) : null;
            response.Dispose();

            if (attempt == 0 && problem?.Type == BadNonceProblemType)
            {
                continue;
            }

            if (attempt == 0 && rateLimitWait is { } wait)
            {
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                continue;
            }

            throw new InvalidOperationException(
                $"ACME request to {url} failed: {problem?.Detail ?? problem?.Type ?? "unknown error"}");
        }

        throw new InvalidOperationException($"ACME request to {url} exhausted its retry attempt.");
    }

    private static string? ReplayNonce(HttpResponseHeaders headers) =>
        headers.TryGetValues("Replay-Nonce", out System.Collections.Generic.IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : null;
}
