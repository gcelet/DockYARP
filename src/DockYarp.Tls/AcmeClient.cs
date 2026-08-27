namespace DockYarp.Tls;

using System;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using DockYarp.Core.Models;
using DockYarp.Tls.Acme;

/// <summary>Hand-rolled ACME v2 (RFC 8555) client, supporting the HTTP-01 and DNS-01 challenges.</summary>
/// <remarks>Performs the real network exchange with the CA; exercised via integration only, not unit tests
/// (mirrors the prior Certes-backed client's own established pattern — <c>AcmeJwsTests</c>/
/// <c>AcmeHttpClientTests</c> cover the parts testable without a live CA). The account key is persisted and
/// reused per (contact email, ACME directory endpoint) pair via <see cref="AcmeAccountKeyStore"/> — accounts
/// are not created fresh per request.</remarks>
/// <param name="options">TLS options (ACME directory, contact email, ToS).</param>
/// <param name="challenges">The HTTP-01 challenge store.</param>
/// <param name="dnsChallenges">The DNS-01 challenge provider (RFC 2136).</param>
/// <param name="fileSystem">Filesystem abstraction used to load/persist the ACME account key.</param>
public sealed class AcmeClient(
    TlsOptions options, IHttp01ChallengeStore challenges, IDnsChallengeProvider dnsChallenges, IFileSystem fileSystem)
    : IAcmeClient
{
    /// <inheritdoc />
    public async Task<LoadedCertificate> RequestCertificateAsync(
        string host, string? email, AcmeChallengeType challengeType, CancellationToken cancellationToken)
    {
        string contact = email ?? options.ContactEmail
            ?? throw new InvalidOperationException("An ACME contact email is required.");

        using ECDsa accountKey = AcmeAccountKeyStore.LoadOrCreate(
            fileSystem, options.CertificateDirectory, options.AcmeDirectoryUri, contact);
        using ECDsa leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using HttpClient httpClient = new();
        AcmeHttpClient acme = new(httpClient, options.AcmeDirectoryUri, accountKey);

        await acme.CreateAccountAsync(
            new AcmeNewAccountRequest { TermsOfServiceAgreed = options.AcceptTermsOfService, Contact = [$"mailto:{contact}"] },
            cancellationToken).ConfigureAwait(false);

        AcmeOrderCreated created = await acme.CreateOrderAsync(host, cancellationToken).ConfigureAwait(false);
        ChallengeContext context = new(acme, created.Order.Authorizations[0], accountKey);
        AcmeAuthorization authorization = await acme.GetAuthorizationAsync(context.AuthorizationUrl, cancellationToken).ConfigureAwait(false);

        if (challengeType == AcmeChallengeType.Dns01)
        {
            await CompleteDnsChallengeAsync(context, authorization, host, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await CompleteHttpChallengeAsync(context, authorization, cancellationToken).ConfigureAwait(false);
        }

        byte[] csr = BuildCsr(host, leafKey);
        await acme.FinalizeOrderAsync(created.Order.Finalize, csr, cancellationToken).ConfigureAwait(false);
        string certificateUrl = await WaitForCertificateAsync(acme, created.OrderUrl, cancellationToken).ConfigureAwait(false);
        string pemChain = await acme.DownloadCertificateChainAsync(certificateUrl, cancellationToken).ConfigureAwait(false);
        return BuildLoadedCertificate(pemChain, leafKey);
    }

    private async Task CompleteHttpChallengeAsync(ChallengeContext context, AcmeAuthorization authorization, CancellationToken cancellationToken)
    {
        AcmeChallenge challenge = authorization.Challenges.First(c => c.Type == "http-01");
        string keyAuthorization = AcmeJws.KeyAuthorization(challenge.Token, context.AccountKey);
        challenges.Set(challenge.Token, keyAuthorization);
        try
        {
            await context.Acme.TriggerChallengeAsync(challenge.Url, cancellationToken).ConfigureAwait(false);
            await WaitForValidationAsync(context.Acme, context.AuthorizationUrl, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            challenges.Remove(challenge.Token);
        }
    }

    private async Task CompleteDnsChallengeAsync(ChallengeContext context, AcmeAuthorization authorization, string host, CancellationToken cancellationToken)
    {
        AcmeChallenge challenge = authorization.Challenges.First(c => c.Type == "dns-01");
        string baseDomain = host.StartsWith("*.", StringComparison.Ordinal) ? host[2..] : host;
        string fqdn = $"_acme-challenge.{baseDomain}";
        string txtValue = AcmeJws.Dns01TxtValue(challenge.Token, context.AccountKey);

        await dnsChallenges.PublishTxtRecordAsync(fqdn, txtValue, cancellationToken).ConfigureAwait(false);
        try
        {
            await context.Acme.TriggerChallengeAsync(challenge.Url, cancellationToken).ConfigureAwait(false);
            await WaitForValidationAsync(context.Acme, context.AuthorizationUrl, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await dnsChallenges.RemoveTxtRecordAsync(fqdn, txtValue, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The ACME wire client, the target authorization's own URL, and the account key signing every
    /// request — bundled to keep <see cref="CompleteHttpChallengeAsync"/>/<see cref="CompleteDnsChallengeAsync"/>
    /// within the 5-parameter guideline.</summary>
    private readonly record struct ChallengeContext(AcmeHttpClient Acme, string AuthorizationUrl, ECDsa AccountKey);

    private static byte[] BuildCsr(string host, ECDsa key) =>
        new CertificateRequest($"CN={host}", key, HashAlgorithmName.SHA256).CreateSigningRequest();

    /// <summary>Assembles the issued leaf (keyed) and every issuer certificate the ACME server returned.</summary>
    /// <param name="pemChain">The concatenated PEM chain (leaf first, per RFC 8555 §7.4.2), no private keys.</param>
    /// <param name="leafKey">The leaf's private key (the CSR's own key, so it always matches the leaf —
    /// no candidate search is needed the way <see cref="PemCertificateLoader"/> needs one).</param>
    /// <returns>The keyed leaf plus every issuer certificate returned, regardless of whether a self-signed
    /// root is among them.</returns>
    /// <remarks>
    /// Deliberately does not require building a PKIX path to a self-signed root: a private CA following
    /// normal ACME convention (root trusted out of band, never sent in the response) will never have one
    /// among the returned certificates. Importing the PEM chain directly and keying only the first entry
    /// needs no root and drops nothing the CA actually sent.
    /// </remarks>
    internal static LoadedCertificate BuildLoadedCertificate(string pemChain, ECDsa leafKey)
    {
        X509Certificate2Collection imported = [];
        imported.ImportFromPem(pemChain);

        X509Certificate2Collection bag = [imported[0].CopyWithPrivateKey(leafKey)];
        try
        {
            for (int i = 1; i < imported.Count; i++)
            {
                bag.Add(imported[i]);
            }

            // Never null: exporting a non-empty X509Certificate2Collection as PKCS12 always produces bytes.
            byte[] pkcs12 = bag.Export(X509ContentType.Pkcs12)!;
            return CertificateCollectionLoader.LoadKeyed(pkcs12, null);
        }
        finally
        {
            foreach (X509Certificate2 entry in bag)
            {
                entry.Dispose();
            }

            foreach (X509Certificate2 entry in imported)
            {
                entry.Dispose();
            }
        }
    }

    private static async Task WaitForValidationAsync(AcmeHttpClient acme, string authorizationUrl, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            AcmeAuthorization authorization = await acme.GetAuthorizationAsync(authorizationUrl, cancellationToken).ConfigureAwait(false);
            if (authorization.Status == "valid")
            {
                return;
            }

            if (authorization.Status == "invalid")
            {
                throw new InvalidOperationException("ACME authorization failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for ACME authorization.");
    }

    private static async Task<string> WaitForCertificateAsync(AcmeHttpClient acme, string orderUrl, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            AcmeOrder order = await acme.GetOrderAsync(orderUrl, cancellationToken).ConfigureAwait(false);
            if (order.Certificate is { } certificateUrl)
            {
                return certificateUrl;
            }

            if (order.Status == "invalid")
            {
                throw new InvalidOperationException("ACME order finalization failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for the ACME order to finalize.");
    }
}
