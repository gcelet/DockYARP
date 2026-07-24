namespace DockYarp.Tls;

using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using Certes.Pkcs;

/// <summary>ACME client backed by Certes, using the HTTP-01 challenge.</summary>
/// <remarks>Performs the real network exchange with the CA; exercised via integration only, not unit tests.</remarks>
/// <param name="options">TLS options (ACME directory, contact email, ToS).</param>
/// <param name="challenges">The HTTP-01 challenge store.</param>
public sealed class CertesAcmeClient(TlsOptions options, IHttp01ChallengeStore challenges) : IAcmeClient
{
    /// <inheritdoc />
    public async Task<X509Certificate2> RequestCertificateAsync(string host, string? email, CancellationToken cancellationToken)
    {
        string contact = email ?? options.ContactEmail
            ?? throw new InvalidOperationException("An ACME contact email is required.");

        AcmeContext acme = new(options.AcmeDirectoryUri);
        await acme.NewAccount(contact, options.AcceptTermsOfService).ConfigureAwait(false);

        IOrderContext order = await acme.NewOrder([host]).ConfigureAwait(false);
        IAuthorizationContext authorization = (await order.Authorizations().ConfigureAwait(false)).First();
        IChallengeContext challenge = await authorization.Http().ConfigureAwait(false);

        challenges.Set(challenge.Token, challenge.KeyAuthz);
        try
        {
            await challenge.Validate().ConfigureAwait(false);
            await WaitForValidationAsync(authorization, cancellationToken).ConfigureAwait(false);

            IKey privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
            CertificateChain chain = await order.Generate(new CsrInfo { CommonName = host }, privateKey).ConfigureAwait(false);
            byte[] pfx = BuildPfx(chain, privateKey, host);
            return X509CertificateLoader.LoadPkcs12(pfx, string.Empty);
        }
        finally
        {
            challenges.Remove(challenge.Token);
        }
    }

    /// <summary>Builds the PFX from the issued chain, falling back to the leaf when the root is not in the chain.</summary>
    /// <param name="chain">The certificate chain returned by the ACME order.</param>
    /// <param name="privateKey">The certificate's private key.</param>
    /// <param name="host">The host used as the PFX friendly name.</param>
    /// <returns>The PFX bytes.</returns>
    /// <remarks>
    /// Public CAs (for example Let's Encrypt) resolve to a root Certes knows, so the full chain is bundled. A
    /// private or custom CA (for example step-ca) does not publish its root in the issued chain, so Certes'
    /// full-chain build fails; the fallback bundles the leaf only (clients trust the CA out of band).
    /// </remarks>
    private static byte[] BuildPfx(CertificateChain chain, IKey privateKey, string host)
    {
        PfxBuilder pfxBuilder = chain.ToPfx(privateKey);
        try
        {
            return pfxBuilder.Build(host, string.Empty);
        }
        catch (AcmeException)
        {
            pfxBuilder.FullChain = false;
            return pfxBuilder.Build(host, string.Empty);
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
