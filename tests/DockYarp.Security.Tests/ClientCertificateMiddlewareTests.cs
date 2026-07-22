namespace DockYarp.Security.Tests;

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Core.Configuration;
using DockYarp.Core.Models;

using Microsoft.AspNetCore.Http;

/// <summary>Tests for <see cref="ClientCertificateMiddleware"/>.</summary>
public sealed class ClientCertificateMiddlewareTests
{
    /// <summary>A required route with no client certificate is rejected with 403.</summary>
    [Test]
    public async Task RequiredWithoutCertificateIsForbidden()
    {
        ClientCertificateMiddleware middleware = Middleware(ClientCertificateRequirement.Required);
        DefaultHttpContext context = SecurityTestHelpers.Context("https", "app.local", "/");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        nextCalled.Should().BeFalse();
    }

    /// <summary>A required route with a presented client certificate continues.</summary>
    [Test]
    public async Task RequiredWithCertificateContinues()
    {
        ClientCertificateMiddleware middleware = Middleware(ClientCertificateRequirement.Required);
        DefaultHttpContext context = SecurityTestHelpers.Context("https", "app.local", "/");
        using X509Certificate2 client = SelfSigned();
        context.Connection.ClientCertificate = client;
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
    }

    /// <summary>A route with no requirement continues without a certificate.</summary>
    [Test]
    public async Task NoRequirementContinues()
    {
        ClientCertificateMiddleware middleware = Middleware(ClientCertificateRequirement.None);
        DefaultHttpContext context = SecurityTestHelpers.Context("https", "app.local", "/");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
    }

    private static ClientCertificateMiddleware Middleware(ClientCertificateRequirement requirement)
    {
        RouteLookup lookup = new(
            SecurityTestHelpers.StoreWith(new RouteRule
            {
                HostPattern = "app.local",
                ClusterId = "app",
                ClientCertificate = requirement,
            }),
            new RoutingOptions());
        return new ClientCertificateMiddleware(lookup);
    }

    private static X509Certificate2 SelfSigned()
    {
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new("CN=client", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }
}
