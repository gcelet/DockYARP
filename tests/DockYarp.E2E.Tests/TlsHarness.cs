namespace DockYarp.E2E.Tests;

using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using DockYarp.E2E.AppHost;

using global::Grpc.Net.Client;

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
        PrepareCertsDirectory();

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
        using X509Certificate2 leafWithKey = leaf.CopyWithPrivateKey(leafKey);

        // On Windows, SChannel cannot use the ephemeral CNG key produced by CopyWithPrivateKey for SslStream client
        // authentication (it fails with SEC_E_UNKNOWN_CREDENTIALS). Round-trip through PKCS#12 so the private key
        // lands in a key set the platform TLS stack accepts (a no-op on Linux/OpenSSL).
        clientCertificate = X509CertificateLoader.LoadPkcs12(
            leafWithKey.Export(X509ContentType.Pkcs12), password: null);
    }

    private static void PrepareCertsDirectory()
    {
        // A world-writable directory DockYarp persists certs + DP keys into (bind-mounted at /certs). World-writable
        // so the non-root container (the app user) can write to the host mount.
        //
        // Best-effort reset: the container writes state here as a different uid (the app user), so the host cannot
        // always delete it — a container-created dataprotection-keys/ subdir is owned by that uid with no write for
        // the host, so its key files are not host-removable. Swallow those permission errors and reuse the leftover
        // state: DockYarp reloads or overwrites it, and the top directory is recreated world-writable below so the
        // container can still write. (Certificate .pfx files sit in this top directory and remain host-deletable.)
        try
        {
            if (Directory.Exists(E2EPaths.CertsDirectory))
            {
                Directory.Delete(E2EPaths.CertsDirectory, recursive: true);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // A previous run's non-root container files remain; they are harmless and get reloaded/overwritten.
        }
        catch (IOException)
        {
            // Same rationale: a container-owned file may not be host-removable; proceed with the existing directory.
        }

        Directory.CreateDirectory(E2EPaths.CertsDirectory);
        if (!OperatingSystem.IsWindows())
        {
            const UnixFileMode worldWritable =
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
            File.SetUnixFileMode(E2EPaths.CertsDirectory, worldWritable);
        }
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
    internal static HttpClient CreateClient(ServerCertificateHolder capture) =>
        Build(capture, presentedCertificate: null, SslProtocols.None);

    /// <summary>Creates an HTTPS client pinned to a specific TLS protocol version (to probe per-host SSL_POLICY).</summary>
    /// <param name="capture">Holder the presented server certificate is stored into.</param>
    /// <param name="enabledProtocols">The only TLS protocol version(s) the client will offer.</param>
    /// <returns>An HTTP client whose handshake fails when the server refuses <paramref name="enabledProtocols"/>.</returns>
    internal static HttpClient CreateClientWithProtocol(ServerCertificateHolder capture, SslProtocols enabledProtocols) =>
        Build(capture, presentedCertificate: null, enabledProtocols);

    /// <summary>Creates an HTTPS client that presents the mutual-TLS client certificate.</summary>
    /// <param name="capture">Holder the presented server certificate is stored into.</param>
    /// <returns>An HTTP client for absolute <c>https://&lt;vhost&gt;/</c> requests, carrying the client certificate.</returns>
    internal static HttpClient CreateMutualTlsClient(ServerCertificateHolder capture) =>
        Build(capture, ClientCertificate, SslProtocols.None);

    /// <summary>Creates a plain HTTP client for DockYarp that does not follow redirects.</summary>
    /// <returns>An HTTP client bound to DockYarp's HTTP endpoint with automatic redirects disabled.</returns>
    internal static HttpClient CreateNonRedirectingHttpClient() =>
        new(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = AspireAppHostFixture.Proxy.BaseAddress };

    /// <summary>Creates a gRPC channel that reaches DockYarp over HTTP/2 (ALPN h2), keeping <paramref name="host"/> as SNI.</summary>
    /// <param name="host">The gRPC virtual host (drives SNI and the <c>:authority</c>).</param>
    /// <param name="capture">Holder the presented server certificate is stored into.</param>
    /// <returns>A gRPC channel whose calls proxy through DockYarp to the labeled gRPC backend.</returns>
    internal static GrpcChannel CreateGrpcChannel(string host, ServerCertificateHolder capture)
    {
        SocketsHttpHandler handler = BuildHandler(capture, presentedCertificate: null, SslProtocols.None);

        // gRPC requires HTTP/2; offer only h2 over ALPN so the handshake negotiates it against DockYarp's TLS.
        handler.SslOptions.ApplicationProtocols = [SslApplicationProtocol.Http2];
        return GrpcChannel.ForAddress($"https://{host}", new GrpcChannelOptions { HttpHandler = handler, DisposeHttpClient = true });
    }

    // The virtual hosts only resolve inside the Docker network, and SNI must carry the virtual host, so the
    // request URI keeps the vhost (driving SNI and the Host header) while the connect callback dials
    // DockYarp's real HTTPS endpoint.
    private static HttpClient Build(
        ServerCertificateHolder capture, X509Certificate2? presentedCertificate, SslProtocols enabledProtocols) =>
        new(BuildHandler(capture, presentedCertificate, enabledProtocols));

    private static SocketsHttpHandler BuildHandler(
        ServerCertificateHolder capture, X509Certificate2? presentedCertificate, SslProtocols enabledProtocols)
    {
        Uri endpoint = AspireAppHostFixture.HttpsBaseAddress;
        SocketsHttpHandler handler = new()
        {
            // No connection reuse: force a fresh TLS handshake per request so a poll observes a certificate
            // provisioned after the first request (a pooled connection keeps the initial fallback certificate).
            PooledConnectionLifetime = TimeSpan.Zero,
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

        if (enabledProtocols != SslProtocols.None)
        {
            // Pin the offered TLS version so the handshake fails when the host's SSL_POLICY forbids it.
            handler.SslOptions.EnabledSslProtocols = enabledProtocols;
        }

        if (presentedCertificate is not null)
        {
            handler.SslOptions.ClientCertificates = [presentedCertificate];
        }

        return handler;
    }

    /// <summary>Captures the server certificate presented during a TLS handshake.</summary>
    internal sealed class ServerCertificateHolder
    {
        /// <summary>Gets or sets the last server certificate presented to the client.</summary>
        public X509Certificate2? ServerCertificate { get; set; }
    }
}
