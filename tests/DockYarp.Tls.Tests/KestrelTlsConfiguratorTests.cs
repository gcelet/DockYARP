namespace DockYarp.Tls.Tests;

using System;
using System.IO.Abstractions.TestingHelpers;

using AwesomeAssertions;

using DockYarp.Tls;

using Microsoft.AspNetCore.Server.Kestrel.Core;

/// <summary>Tests for <see cref="KestrelTlsConfigurator"/>.</summary>
public sealed class KestrelTlsConfiguratorTests
{
    /// <summary>Configuring HTTPS defaults (default cert + SNI selector) does not throw.</summary>
    [Test]
    public void ConfigureRegistersHttpsDefaults()
    {
        FakeCertificateStore store = new();
        TlsOptions tlsOptions = new();
        using DefaultCertificateProvider fallback = new(tlsOptions, new MockFileSystem());
        KestrelTlsConfigurator configurator = new(
            new SniCertificateSelector(store, fallback),
            fallback,
            tlsOptions,
            new ClientCertificateValidator(tlsOptions, new MockFileSystem()));
        KestrelServerOptions options = new();

        Action configure = () => configurator.Configure(options);

        configure.Should().NotThrow();
    }
}
