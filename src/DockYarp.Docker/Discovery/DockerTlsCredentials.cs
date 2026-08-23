namespace DockYarp.Docker.Discovery;

using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using global::Docker.DotNet.Handler.Abstractions;
using global::Docker.DotNet.X509;

/// <summary>Builds Docker.DotNet credentials for a TLS daemon connection (client certificate + CA verification).</summary>
/// <remarks>
/// Wraps <see cref="CertificateCredentials"/> (from <c>Docker.DotNet.Enhanced.X509</c>), which attaches the
/// client certificate and validation callback onto whichever transport handler the client resolves
/// (<see cref="System.Net.Http.SocketsHttpHandler"/> for the native HTTP transport DockYarp uses). Inputs are
/// PEM strings (not paths), so the factory is testable without touching the filesystem.
/// </remarks>
public static class DockerTlsCredentials
{
    /// <summary>Creates TLS credentials from PEM material, or <see langword="null"/> when TLS does not apply.</summary>
    /// <param name="endpoint">The Docker endpoint; client TLS applies only to a <c>tcp://</c> URL.</param>
    /// <param name="verification">How to verify the daemon certificate.</param>
    /// <param name="caPem">The CA bundle (PEM) used to verify the daemon; may be <see langword="null"/>.</param>
    /// <param name="certPem">The client certificate (PEM).</param>
    /// <param name="keyPem">The client private key (PEM).</param>
    /// <returns>TLS credentials, or <see langword="null"/> to connect unchanged (socket / no client certificate).</returns>
    public static IAuthProvider? Create(
        Uri? endpoint, DaemonTlsVerification verification, string? caPem, string? certPem, string? keyPem)
    {
        if (!UsesTls(endpoint) || string.IsNullOrEmpty(certPem) || string.IsNullOrEmpty(keyPem))
        {
            return null;
        }

        X509Certificate2 clientCertificate = LoadClientCertificate(certPem, keyPem);
        return new DisposableCertificateCredentials(clientCertificate, BuildServerValidation(verification, caPem));
    }

    private static bool UsesTls(Uri? endpoint) =>
        endpoint is not null && string.Equals(endpoint.Scheme, "tcp", StringComparison.OrdinalIgnoreCase);

    private static X509Certificate2 LoadClientCertificate(string certPem, string keyPem)
    {
        // Re-import as PKCS#12 so the private key is usable for the TLS handshake on all platforms (Windows
        // SChannel rejects an ephemeral PEM key). Mirrors DockYarp.Tls.PemCertificateLoader (out of this
        // module's dependency graph, so the idiom is repeated rather than referenced).
        using X509Certificate2 pem = X509Certificate2.CreateFromPem(certPem, keyPem);
        return X509CertificateLoader.LoadPkcs12(pem.Export(X509ContentType.Pkcs12), password: null);
    }

    private static RemoteCertificateValidationCallback BuildServerValidation(
        DaemonTlsVerification verification, string? caPem)
    {
        if (verification == DaemonTlsVerification.AcceptAny)
        {
            return static (_, _, _, _) => true;
        }

        X509Certificate2Collection authorities = [];
        if (!string.IsNullOrEmpty(caPem))
        {
            authorities.ImportFromPem(caPem);
        }

        return (_, certificate, _, _) => ChainsToAuthority(certificate, authorities);
    }

    private static bool ChainsToAuthority(X509Certificate? certificate, X509Certificate2Collection authorities)
    {
        if (certificate is not X509Certificate2 server || authorities.Count == 0)
        {
            return false;
        }

        using X509Chain chain = new();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.CustomTrustStore.AddRange(authorities);
        return chain.Build(server);
    }

    // CertificateCredentials itself does not own or dispose the certificate it wraps (confirmed: it has no
    // IDisposable, and DockerClient.Dispose only disposes the HTTP handler) — this wrapper adds that ownership
    // so DockerContainerSource can dispose the credentials alongside the client.
    private sealed class DisposableCertificateCredentials : IAuthProvider, IDisposable
    {
        private readonly X509Certificate2 clientCertificate;
        private readonly CertificateCredentials inner;

        public DisposableCertificateCredentials(
            X509Certificate2 clientCertificate, RemoteCertificateValidationCallback serverValidation)
        {
            this.clientCertificate = clientCertificate;
            inner = new CertificateCredentials(clientCertificate) { ServerCertificateValidationCallback = serverValidation };
        }

        public bool TlsEnabled => inner.TlsEnabled;

        public System.Net.Http.HttpMessageHandler ConfigureHandler(System.Net.Http.HttpMessageHandler handler) =>
            inner.ConfigureHandler(handler);

        public void Dispose() => clientCertificate.Dispose();
    }
}
