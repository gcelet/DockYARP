namespace DockYarp.E2E.Tests;

using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using DockYarp.E2E.AppHost;

/// <summary>TLS test material: an ephemeral client CA/leaf for mutual TLS and a TLS-aware HTTPS client factory.</summary>
/// <remarks>
/// The client CA is generated in memory and its public certificate is written to <see cref="E2EPaths.ClientCaFile"/>,
/// which DockYarp mounts as its client CA — no client-auth key material is committed to the repository.
/// The server-certificate callback accepts the connection but captures the presented certificate so scenarios
/// can distinguish an ACME-issued certificate from the self-signed fallback.
/// </remarks>
internal static class TlsHarness
{
    private static X509Certificate2? clientCertificate;

    /// <summary>Gets the client certificate (with private key) presented in the mutual-TLS scenario.</summary>
    internal static X509Certificate2 ClientCertificate =>
        clientCertificate ?? throw new InvalidOperationException("The client CA has not been prepared.");

    /// <summary>Generates the ephemeral client CA and leaf, writing the CA certificate to the mounted path.</summary>
    internal static void PrepareClientCa()
    {
        Directory.CreateDirectory(E2EPaths.StepCaDirectory);
        Directory.CreateDirectory(E2EPaths.ClientCaDirectory);

        DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-1);
        DateTimeOffset to = DateTimeOffset.UtcNow.AddDays(365);

        using RSA caKey = RSA.Create(2048);
        CertificateRequest caRequest = new(
            "CN=DockYarp E2E Client CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using X509Certificate2 ca = caRequest.CreateSelfSigned(from, to);
        File.WriteAllText(E2EPaths.ClientCaFile, ca.ExportCertificatePem());

        using RSA leafKey = RSA.Create(2048);
        CertificateRequest leafRequest = new(
            "CN=e2e-client", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        leafRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        OidCollection clientAuth = [];
        clientAuth.Add(new Oid("1.3.6.1.5.5.7.3.2"));
        leafRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(clientAuth, false));

        byte[] serial = [1, 2, 3, 4, 5, 6, 7, 8];
        using X509Certificate2 leaf = leafRequest.Create(ca, from, to, serial);
        clientCertificate = leaf.CopyWithPrivateKey(leafKey);
    }

    /// <summary>Disposes the generated client certificate.</summary>
    internal static void Cleanup()
    {
        clientCertificate?.Dispose();
        clientCertificate = null;
    }

    /// <summary>Creates an HTTPS client (no client certificate) that reaches DockYarp keeping the vhost as SNI.</summary>
    /// <param name="capture">Holder the presented server certificate is stored into.</param>
    /// <returns>An HTTP client for absolute <c>https://&lt;vhost&gt;/</c> requests.</returns>
    internal static HttpClient CreateClient(ServerCertificateHolder capture) => Build(capture, presentedCertificate: null);

    /// <summary>Creates an HTTPS client that presents the mutual-TLS client certificate.</summary>
    /// <param name="capture">Holder the presented server certificate is stored into.</param>
    /// <returns>An HTTP client for absolute <c>https://&lt;vhost&gt;/</c> requests, carrying the client certificate.</returns>
    internal static HttpClient CreateMutualTlsClient(ServerCertificateHolder capture) => Build(capture, ClientCertificate);

    /// <summary>Creates a plain HTTP client for DockYarp that does not follow redirects.</summary>
    /// <returns>An HTTP client bound to DockYarp's HTTP endpoint with automatic redirects disabled.</returns>
    internal static HttpClient CreateNonRedirectingHttpClient() =>
        new(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = AspireAppHostFixture.Proxy.BaseAddress };

    // The virtual hosts only resolve inside the Docker network, and SNI must carry the virtual host, so the
    // request URI keeps the vhost (driving SNI and the Host header) while the connect callback dials
    // DockYarp's real HTTPS endpoint.
    private static HttpClient Build(ServerCertificateHolder capture, X509Certificate2? presentedCertificate)
    {
        Uri endpoint = AspireAppHostFixture.HttpsBaseAddress;
        SocketsHttpHandler handler = new()
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                {
                    capture.ServerCertificate = certificate is null
                        ? null
                        : X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
                    return true;
                },
            },
            ConnectCallback = async (_, cancellationToken) =>
            {
                Socket socket = new(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                await socket.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            },
        };

        if (presentedCertificate is not null)
        {
            handler.SslOptions.ClientCertificates = [presentedCertificate];
        }

        return new HttpClient(handler);
    }

    /// <summary>Captures the server certificate presented during a TLS handshake.</summary>
    internal sealed class ServerCertificateHolder
    {
        /// <summary>Gets or sets the last server certificate presented to the client.</summary>
        public X509Certificate2? ServerCertificate { get; set; }
    }
}
