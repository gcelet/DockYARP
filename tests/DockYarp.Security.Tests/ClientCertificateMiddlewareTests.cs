namespace DockYarp.Security.Tests;

using System;
using System.IO.Abstractions.TestingHelpers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using AwesomeAssertions;

using DockYarp.Core.Configuration;
using DockYarp.Core.Models;
using DockYarp.Tls;

using Microsoft.AspNetCore.Http;

/// <summary>Tests for <see cref="ClientCertificateMiddleware"/>.</summary>
public sealed class ClientCertificateMiddlewareTests
{
    // A fixed validity window shared by the CA and the issued client certificate (see ClientCertificateValidatorTests).
    private static readonly DateTimeOffset NotBefore = DateTimeOffset.UtcNow.AddDays(-1);
    private static readonly DateTimeOffset NotAfter = DateTimeOffset.UtcNow.AddYears(1);

    /// <summary>A required route with no client certificate is rejected with 403.</summary>
    [Test]
    public async Task RequiredWithoutCertificateIsForbidden()
    {
        using X509Certificate2 ca = CreateCa();
        ClientCertificateMiddleware middleware = Middleware(ClientCertificateRequirement.Required, ca);
        DefaultHttpContext context = SecurityTestHelpers.Context("https", "app.local", "/");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        nextCalled.Should().BeFalse();
        context.Items[ClientCertificateMiddleware.VerificationStatusKey]
            .Should().Be(ClientCertificateVerificationStatus.NotPresented);
    }

    /// <summary>A required route with a certificate chaining to the configured CA continues.</summary>
    [Test]
    public async Task RequiredWithValidCertificateContinues()
    {
        using X509Certificate2 ca = CreateCa();
        using X509Certificate2 client = IssueClientCertificate(ca);
        ClientCertificateMiddleware middleware = Middleware(ClientCertificateRequirement.Required, ca);
        DefaultHttpContext context = SecurityTestHelpers.Context("https", "app.local", "/");
        context.Connection.ClientCertificate = client;
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
        context.Items[ClientCertificateMiddleware.VerificationStatusKey]
            .Should().Be(ClientCertificateVerificationStatus.Verified);
    }

    /// <summary>An optional route continues regardless of the certificate's verification outcome, but still
    /// records the real status for downstream header forwarding.</summary>
    [Test]
    public async Task OptionalContinuesForEveryStatusButRecordsIt()
    {
        using X509Certificate2 ca = CreateCa();
        using X509Certificate2 client = IssueClientCertificate(ca);
        using X509Certificate2 untrusted = DefaultCertificateFactory.CreateSelfSigned("intruder.local");
        ClientCertificateMiddleware middleware = Middleware(ClientCertificateRequirement.Optional, ca);

        DefaultHttpContext verified = SecurityTestHelpers.Context("https", "app.local", "/");
        verified.Connection.ClientCertificate = client;
        await middleware.InvokeAsync(verified, _ => Task.CompletedTask);
        verified.Items[ClientCertificateMiddleware.VerificationStatusKey]
            .Should().Be(ClientCertificateVerificationStatus.Verified);

        DefaultHttpContext failed = SecurityTestHelpers.Context("https", "app.local", "/");
        failed.Connection.ClientCertificate = untrusted;
        await middleware.InvokeAsync(failed, _ => Task.CompletedTask);
        failed.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        failed.Items[ClientCertificateMiddleware.VerificationStatusKey]
            .Should().Be(ClientCertificateVerificationStatus.Failed);

        DefaultHttpContext none = SecurityTestHelpers.Context("https", "app.local", "/");
        await middleware.InvokeAsync(none, _ => Task.CompletedTask);
        none.Items[ClientCertificateMiddleware.VerificationStatusKey]
            .Should().Be(ClientCertificateVerificationStatus.NotPresented);
    }

    /// <summary>A route with no requirement continues without a certificate and records no status.</summary>
    [Test]
    public async Task NoRequirementContinues()
    {
        using X509Certificate2 ca = CreateCa();
        ClientCertificateMiddleware middleware = Middleware(ClientCertificateRequirement.None, ca);
        DefaultHttpContext context = SecurityTestHelpers.Context("https", "app.local", "/");
        bool nextCalled = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
        context.Items.ContainsKey(ClientCertificateMiddleware.VerificationStatusKey).Should().BeFalse();
    }

    private static ClientCertificateMiddleware Middleware(ClientCertificateRequirement requirement, X509Certificate2 ca)
    {
        RouteLookup lookup = new(
            SecurityTestHelpers.StoreWith(new RouteRule
            {
                HostPattern = "app.local",
                ClusterId = "app",
                ClientCertificate = requirement,
            }),
            new RoutingOptions());

        MockFileSystem fileSystem = new();
        string caPath = fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), "ca.crt");
        fileSystem.AddFile(caPath, new MockFileData(ca.ExportCertificatePem()));
        ClientCertificateValidator validator = new(new TlsOptions { ClientCaCertificatePath = caPath }, fileSystem);

        return new ClientCertificateMiddleware(lookup, validator);
    }

    private static X509Certificate2 CreateCa()
    {
        using RSA caKey = RSA.Create(2048);
        CertificateRequest request = new("CN=Test CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        return request.CreateSelfSigned(NotBefore, NotAfter);
    }

    private static X509Certificate2 IssueClientCertificate(X509Certificate2 ca)
    {
        using RSA clientKey = RSA.Create(2048);
        CertificateRequest request = new("CN=client", clientKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        return request.Create(ca, NotBefore, NotAfter, [1, 2, 3, 4]);
    }
}
