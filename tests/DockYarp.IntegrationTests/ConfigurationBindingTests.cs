namespace DockYarp.IntegrationTests;

using System;
using System.Collections.Generic;

using AwesomeAssertions;

using DockYarp.AdminApi;
using DockYarp.Security;
using DockYarp.Tls;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Verifies that host options are bound from configuration.</summary>
public sealed class ConfigurationBindingTests
{
    /// <summary>TLS, security, and admin options reflect configuration values.</summary>
    [Test]
    public void OptionsAreBoundFromConfiguration()
    {
        Dictionary<string, string?> settings = new(StringComparer.Ordinal)
        {
            ["Tls:AcmeDirectoryUri"] = "https://acme.example.com/directory",
            ["Tls:AcceptTermsOfService"] = "true",
            ["Security:FrameOptions"] = "SAMEORIGIN",
            ["AdminApi:ApiKey"] = "configured-key",
        };
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                foreach (KeyValuePair<string, string?> setting in settings)
                {
                    builder.UseSetting(setting.Key, setting.Value);
                }
            });

        TlsOptions tls = factory.Services.GetRequiredService<TlsOptions>();
        SecurityHeadersOptions security = factory.Services.GetRequiredService<SecurityHeadersOptions>();
        AdminApiOptions admin = factory.Services.GetRequiredService<AdminApiOptions>();

        tls.AcmeDirectoryUri.Should().Be(new Uri("https://acme.example.com/directory"));
        tls.AcceptTermsOfService.Should().BeTrue();
        security.FrameOptions.Should().Be("SAMEORIGIN");
        admin.ApiKey.Should().Be("configured-key");
    }

    /// <summary>Defaults are preserved when configuration is absent.</summary>
    [Test]
    public void DefaultsPreservedWhenUnset()
    {
        using WebApplicationFactory<Program> factory = new();

        TlsOptions tls = factory.Services.GetRequiredService<TlsOptions>();

        tls.AcmeDirectoryUri.Host.Should().Be("acme-staging-v02.api.letsencrypt.org");
    }
}
