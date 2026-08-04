namespace DockYarp.Docker.Discovery;

using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using global::Docker.DotNet;

using Microsoft.Net.Http.Client;

/// <summary>Builds Docker.DotNet credentials for a TLS daemon connection (client certificate + CA verification).</summary>
/// <remarks>
/// The pinned Docker.DotNet has no <c>CertificateCredentials</c>, so a small custom <see cref="Credentials"/>
/// wires the client certificate and validation callback onto Docker.DotNet's public <see cref="ManagedHandler"/>.
/// Inputs are PEM strings (not paths), so the factory is testable without touching the filesystem.
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
    public static Credentials? Create(
        Uri? endpoint, DaemonTlsVerification verification, string? caPem, string? certPem, string? keyPem)
    {
        if (!UsesTls(endpoint) || string.IsNullOrEmpty(certPem) || string.IsNullOrEmpty(keyPem))
        {
            return null;
        }

        X509Certificate2 clientCertificate = LoadClientCertificate(certPem, keyPem);
        return new ClientCertificateCredentials(clientCertificate, BuildServerValidation(verification, caPem));
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

    // Docker.DotNet builds a ManagedHandler and calls GetHandler(it), so casting wires the live handler.
    private sealed class ClientCertificateCredentials(
        X509Certificate2 clientCertificate, RemoteCertificateValidationCallback serverValidation) : Credentials
    {
        public override bool IsTlsCredentials() => true;

        public override HttpMessageHandler GetHandler(HttpMessageHandler innerHandler)
        {
            if (innerHandler is ManagedHandler managed)
            {
                managed.ClientCertificates = new X509CertificateCollection { clientCertificate };
                managed.ServerCertificateValidationCallback = serverValidation;
            }

            return innerHandler;
        }

        public override void Dispose()
        {
            clientCertificate.Dispose();
            base.Dispose();
        }
    }
}
