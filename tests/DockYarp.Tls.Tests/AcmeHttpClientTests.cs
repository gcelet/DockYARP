namespace DockYarp.Tls.Tests;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Tls.Acme;

/// <summary>Tests the pieces of the hand-rolled ACME wire client that don't need a live CA: replay-nonce
/// lifecycle and the bounded <c>badNonce</c> retry RFC 8555 §6.7 documents. Uses a queued fake
/// <see cref="HttpMessageHandler"/> — no external library or live server involved, mirroring
/// <c>AcmeJwsTests</c>'s own self-contained approach.</summary>
public sealed class AcmeHttpClientTests
{
    private const string DirectoryJson = """
        {"newNonce":"https://acme.example/new-nonce","newAccount":"https://acme.example/new-account","newOrder":"https://acme.example/new-order"}
        """;

    private static AcmeHttpClient NewClient(QueueHandler handler) =>
        new(new HttpClient(handler), new Uri("https://acme.example/directory"), ECDsa.Create(ECCurve.NamedCurves.nistP256));

    [Test]
    public async Task CreateOrder_FetchesNonceOnceThenReusesTheOneFromEachResponse()
    {
        QueueHandler handler = new(
        [
            _ => JsonResponse(HttpStatusCode.OK, DirectoryJson, nonce: null),
            _ => NonceOnly("nonce-A"),
            _ => OrderCreatedResponse("""{"status":"pending","authorizations":[],"finalize":"https://acme.example/finalize/1"}""", "nonce-B"),
        ]);
        AcmeHttpClient client = NewClient(handler);

        await client.CreateOrderAsync("app.local", CancellationToken.None);

        handler.Requests.Should().HaveCount(3, "directory, one nonce fetch, one signed POST");
        handler.Requests[1].Method.Should().Be(HttpMethod.Head, "the nonce endpoint is polled with HEAD");
    }

    [Test]
    public async Task SendSigned_RetriesOnceOnBadNonceThenSucceeds()
    {
        QueueHandler handler = new(
        [
            _ => JsonResponse(HttpStatusCode.OK, DirectoryJson, nonce: null),
            _ => NonceOnly("nonce-A"),
            _ => JsonResponse(HttpStatusCode.BadRequest, """{"type":"urn:ietf:params:acme:error:badNonce","detail":"stale"}""", "nonce-B"),
            _ => OrderCreatedResponse("""{"status":"pending","authorizations":[],"finalize":"https://acme.example/finalize/1"}""", "nonce-C"),
        ]);
        AcmeHttpClient client = NewClient(handler);

        AcmeOrderCreated created = await client.CreateOrderAsync("app.local", CancellationToken.None);

        created.OrderUrl.Should().Be("https://acme.example/order/1");
        created.Order.Status.Should().Be("pending");
        handler.Requests.Should().HaveCount(4, "directory, nonce fetch, one failed POST, one retried POST");
    }

    [Test]
    public async Task SendSigned_ThrowsImmediatelyOnANonBadNonceError()
    {
        QueueHandler handler = new(
        [
            _ => JsonResponse(HttpStatusCode.OK, DirectoryJson, nonce: null),
            _ => NonceOnly("nonce-A"),
            _ => JsonResponse(HttpStatusCode.Forbidden, """{"type":"urn:ietf:params:acme:error:unauthorized","detail":"nope"}""", "nonce-B"),
        ]);
        AcmeHttpClient client = NewClient(handler);

        Func<Task> act = async () => await client.CreateOrderAsync("app.local", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*nope*");
        handler.Requests.Should().HaveCount(3, "no retry for a non-badNonce error");
    }

    [Test]
    public async Task SendSigned_ThrowsAfterExhaustingTheSingleBadNonceRetry()
    {
        QueueHandler handler = new(
        [
            _ => JsonResponse(HttpStatusCode.OK, DirectoryJson, nonce: null),
            _ => NonceOnly("nonce-A"),
            _ => JsonResponse(HttpStatusCode.BadRequest, """{"type":"urn:ietf:params:acme:error:badNonce","detail":"stale-1"}""", "nonce-B"),
            _ => JsonResponse(HttpStatusCode.BadRequest, """{"type":"urn:ietf:params:acme:error:badNonce","detail":"stale-2"}""", "nonce-C"),
        ]);
        AcmeHttpClient client = NewClient(handler);

        Func<Task> act = async () => await client.CreateOrderAsync("app.local", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        handler.Requests.Should().HaveCount(4, "exactly one retry attempt, not unbounded");
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json, string? nonce)
    {
        HttpResponseMessage response = new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        if (nonce is not null)
        {
            response.Headers.Add("Replay-Nonce", nonce);
        }

        return response;
    }

    private static HttpResponseMessage OrderCreatedResponse(string json, string nonce)
    {
        HttpResponseMessage response = JsonResponse(HttpStatusCode.Created, json, nonce);
        response.Headers.Location = new Uri("https://acme.example/order/1");
        return response;
    }

    private static HttpResponseMessage NonceOnly(string nonce)
    {
        HttpResponseMessage response = new(HttpStatusCode.NoContent);
        response.Headers.Add("Replay-Nonce", nonce);
        return response;
    }

    private sealed class QueueHandler(IReadOnlyList<Func<HttpRequestMessage, HttpResponseMessage>> responses) : HttpMessageHandler
    {
        private int index;

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responses[index++](request));
        }
    }
}
