namespace DockYarp.Tls;

using System;
using System.IO.Abstractions;

using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<ClientCertificateValidator>();
        services.AddSingleton<DefaultCertificateProvider>();
        services.AddSingleton<ICertificateStore, FileCertificateStore>();
        services.AddSingleton<SniCertificateSelector>();
        services.AddSingleton<SniTlsHandshakeCallback>();
        services.AddSingleton<IHttp01ChallengeStore, Http01ChallengeStore>();
        services.AddSingleton<Http01ChallengeMiddleware>();
        services.AddSingleton<IDnsChallengeProvider, Rfc2136DnsChallengeProvider>();
        services.AddSingleton<IAcmeClient, CertesAcmeClient>();

        // Default: no reserved (non-route) hosts. A host application (e.g. the ASP.NET host, for the admin host)
        // may register its own IReservedCertificateHosts to add hosts to the provisioning loop.
        services.TryAddSingleton<IReservedCertificateHosts, NoReservedCertificateHosts>();
        services.AddHostedService<CertificateProvisioningService>();

        // A default (8080/8443) unless the host already bound endpoint options from the "Server" configuration.
        services.TryAddSingleton<ServerEndpointOptions>();
        services.AddSingleton<IConfigureOptions<KestrelServerOptions>, KestrelTlsConfigurator>();
        return services;
    }
}
