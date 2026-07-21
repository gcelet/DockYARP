namespace DockYarp.Tls;

using System;

using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>Dependency-injection registration for TLS/ACME.</summary>
public static class TlsServiceCollectionExtensions
{
    /// <summary>Registers the certificate store, SNI selector, HTTP-01 challenge, ACME client, and provisioning.</summary>
    /// <remarks>The caller must also register an <c>IRouteConfigStore</c>.</remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="options">TLS options.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddDockYarpTls(this IServiceCollection services, TlsOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddSingleton<DefaultCertificateProvider>();
        services.AddSingleton<ICertificateStore, FileCertificateStore>();
        services.AddSingleton<SniCertificateSelector>();
        services.AddSingleton<IHttp01ChallengeStore, Http01ChallengeStore>();
        services.AddSingleton<Http01ChallengeMiddleware>();
        services.AddSingleton<IAcmeClient, CertesAcmeClient>();
        services.AddHostedService<CertificateProvisioningService>();
        services.AddSingleton<IConfigureOptions<KestrelServerOptions>, KestrelTlsConfigurator>();
        return services;
    }
}
