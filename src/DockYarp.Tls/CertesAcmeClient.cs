namespace DockYarp.Tls;

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using Certes;
using Certes.Acme;
using Certes.Acme.Resource;

using DockYarp.Core.Models;

/// <summary>ACME client backed by Certes, supporting the HTTP-01 and DNS-01 challenges.</summary>
/// <remarks>Performs the real network exchange with the CA; exercised via integration only, not unit tests.</remarks>
/// <param name="options">TLS options (ACME directory, contact email, ToS).</param>
/// <param name="challenges">The HTTP-01 challenge store.</param>
/// <param name="dnsChallenges">The DNS-01 challenge provider (RFC 2136).</param>
public sealed class CertesAcmeClient(TlsOptions options, IHttp01ChallengeStore challenges, IDnsChallengeProvider dnsChallenges) : IAcmeClient
{
    /// <inheritdoc />
    public async Task<LoadedCertificate> RequestCertificateAsync(
        string host, string? email, AcmeChallengeType challengeType, CancellationToken cancellationToken)
    {
        string contact = email ?? options.ContactEmail
            ?? throw new InvalidOperationException("An ACME contact email is required.");

        AcmeContext acme = new(options.AcmeDirectoryUri);
        await acme.NewAccount(contact, options.AcceptTermsOfService).ConfigureAwait(false);

        IOrderContext order = await acme.NewOrder([host]).ConfigureAwait(false);
        IAuthorizationContext authorization = (await order.Authorizations().ConfigureAwait(false)).First();

        IKey privateKey = challengeType == AcmeChallengeType.Dns01
            ? await CompleteDnsChallengeAsync(acme, authorization, host, cancellationToken).ConfigureAwait(false)
            : await CompleteHttpChallengeAsync(authorization, cancellationToken).ConfigureAwait(false);

        CertificateChain chain = await order.Generate(new CsrInfo { CommonName = host }, privateKey).ConfigureAwait(false);
        return BuildLoadedCertificate(chain, privateKey);
    }

    private async Task<IKey> CompleteHttpChallengeAsync(IAuthorizationContext authorization, CancellationToken cancellationToken)
    {
        IChallengeContext challenge = await authorization.Http().ConfigureAwait(false);
        challenges.Set(challenge.Token, challenge.KeyAuthz);
        try
        {
            await challenge.Validate().ConfigureAwait(false);
            await WaitForValidationAsync(authorization, cancellationToken).ConfigureAwait(false);
            return KeyFactory.NewKey(KeyAlgorithm.ES256);
        }
        finally
        {
            challenges.Remove(challenge.Token);
        }
    }

    private async Task<IKey> CompleteDnsChallengeAsync(
        AcmeContext acme, IAuthorizationContext authorization, string host, CancellationToken cancellationToken)
    {
        IChallengeContext challenge = await authorization.Dns().ConfigureAwait(false);
        string baseDomain = host.StartsWith("*.", StringComparison.Ordinal) ? host[2..] : host;
        string fqdn = $"_acme-challenge.{baseDomain}";
        string txtValue = acme.AccountKey.DnsTxt(challenge.Token);

        await dnsChallenges.PublishTxtRecordAsync(fqdn, txtValue, cancellationToken).ConfigureAwait(false);
        try
        {
            await challenge.Validate().ConfigureAwait(false);
            await WaitForValidationAsync(authorization, cancellationToken).ConfigureAwait(false);
            return KeyFactory.NewKey(KeyAlgorithm.ES256);
        }
        finally
        {
            await dnsChallenges.RemoveTxtRecordAsync(fqdn, txtValue, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Assembles the issued leaf (keyed) and every issuer certificate the ACME server returned.</summary>
    /// <param name="chain">The certificate chain returned by the ACME order.</param>
    /// <param name="privateKey">The leaf's private key (the CSR's own key, so it always matches the leaf —
    /// no candidate search is needed the way <see cref="PemCertificateLoader"/> needs one).</param>
    /// <returns>The keyed leaf plus every issuer certificate returned, regardless of whether a self-signed
    /// root is among them.</returns>
    /// <remarks>
    /// Deliberately does not use Certes' <c>PfxBuilder.FullChain</c>: that mode requires building a PKIX path
    /// to a self-signed root found among the returned certificates, which a private CA following normal ACME
    /// convention (root trusted out of band, never sent in the response) will never have — the resulting
    /// path-build failure previously fell back to bundling the leaf only, silently dropping intermediates the
    /// CA did return. Building directly from <see cref="CertificateChain.Certificate"/> and
    /// <see cref="CertificateChain.Issuers"/> needs no root and drops nothing.
    /// </remarks>
    internal static LoadedCertificate BuildLoadedCertificate(CertificateChain chain, IKey privateKey)
    {
        using ECDsa ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privateKey.ToPem());

        using X509Certificate2 leafWithoutKey = X509CertificateLoader.LoadCertificate(chain.Certificate.ToDer());
        X509Certificate2Collection bag = [leafWithoutKey.CopyWithPrivateKey(ecdsa)];
        try
        {
            foreach (IEncodable issuer in chain.Issuers)
            {
                bag.Add(X509CertificateLoader.LoadCertificate(issuer.ToDer()));
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
        }
    }

    private static async Task WaitForValidationAsync(IAuthorizationContext authorization, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            Authorization resource = await authorization.Resource().ConfigureAwait(false);
            if (resource.Status == AuthorizationStatus.Valid)
            {
                return;
            }

            if (resource.Status == AuthorizationStatus.Invalid)
            {
                throw new InvalidOperationException("ACME authorization failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for ACME authorization.");
    }
}
