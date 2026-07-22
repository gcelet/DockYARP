namespace DockYarp.Tls.Tests;

using System;

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
        using DefaultCertificateProvider fallback = new();
        KestrelTlsConfigurator configurator = new(new SniCertificateSelector(store, fallback), fallback);
        KestrelServerOptions options = new();

        Action configure = () => configurator.Configure(options);

        configure.Should().NotThrow();
    }
}
