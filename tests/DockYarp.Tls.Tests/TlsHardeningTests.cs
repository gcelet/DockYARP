namespace DockYarp.Tls.Tests;

using System.Net.Security;
using System.Security.Authentication;

using AwesomeAssertions;

using DockYarp.Tls;

using Microsoft.AspNetCore.Server.Kestrel.Core;

/// <summary>Tests for the pure <see cref="TlsHardening"/> mappings.</summary>
public sealed class TlsHardeningTests
{
    /// <summary>TLS 1.2 as the minimum enables TLS 1.2 and 1.3; TLS 1.3 enables only 1.3.</summary>
    [Test]
    public void MinimumVersionMapsToProtocols()
    {
        TlsHardening.ToSslProtocols(TlsVersion.Tls12).Should().Be(SslProtocols.Tls12 | SslProtocols.Tls13);
        TlsHardening.ToSslProtocols(TlsVersion.Tls13).Should().Be(SslProtocols.Tls13);
    }

    /// <summary>HTTP protocols parse case-insensitively and fall back to HTTP/1.1+2.</summary>
    [Test]
    public void HttpProtocolsParse()
    {
        TlsHardening.ParseHttpProtocols("Http1").Should().Be(HttpProtocols.Http1);
        TlsHardening.ParseHttpProtocols("bogus").Should().Be(HttpProtocols.Http1AndHttp2);
        TlsHardening.ParseHttpProtocols(null).Should().Be(HttpProtocols.Http1AndHttp2);
    }

    /// <summary>Cipher-suite names are parsed and unknown entries are skipped.</summary>
    [Test]
    public void CipherSuitesParseSkippingUnknown()
    {
        var suites = TlsHardening.ParseCipherSuites(["TLS_AES_128_GCM_SHA256", "NOT_A_CIPHER"]);

        suites.Should().ContainSingle().Which.Should().Be(TlsCipherSuite.TLS_AES_128_GCM_SHA256);
    }
}
