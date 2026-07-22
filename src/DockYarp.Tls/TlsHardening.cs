namespace DockYarp.Tls;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net.Security;
using System.Security.Authentication;

using Microsoft.AspNetCore.Server.Kestrel.Core;

/// <summary>Pure mapping of TLS hardening options to platform types.</summary>
/// <remarks>Kept side-effect free so the mappings can be unit tested without starting Kestrel.</remarks>
public static class TlsHardening
{
    /// <summary>Maps the minimum TLS version to the set of enabled <see cref="SslProtocols"/>.</summary>
    /// <param name="minimum">The minimum accepted TLS version.</param>
    /// <returns>The enabled protocols (TLS 1.2 also enables TLS 1.3).</returns>
    public static SslProtocols ToSslProtocols(TlsVersion minimum) =>
        minimum == TlsVersion.Tls13 ? SslProtocols.Tls13 : SslProtocols.Tls12 | SslProtocols.Tls13;

    /// <summary>Parses the configured HTTP protocols, falling back to HTTP/1.1 and HTTP/2.</summary>
    /// <param name="value">The configured value (for example <c>Http1AndHttp2</c>).</param>
    /// <returns>The parsed protocols, or <see cref="HttpProtocols.Http1AndHttp2"/> when unrecognized.</returns>
    public static HttpProtocols ParseHttpProtocols(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out HttpProtocols parsed) ? parsed : HttpProtocols.Http1AndHttp2;

    /// <summary>Parses cipher-suite names into <see cref="TlsCipherSuite"/> values, skipping unknown entries.</summary>
    /// <param name="names">The configured cipher-suite names.</param>
    /// <returns>The recognized cipher suites (empty when none are recognized or configured).</returns>
    public static ImmutableArray<TlsCipherSuite> ParseCipherSuites(IEnumerable<string>? names)
    {
        if (names is null)
        {
            return [];
        }

        ImmutableArray<TlsCipherSuite>.Builder suites = ImmutableArray.CreateBuilder<TlsCipherSuite>();
        foreach (string name in names)
        {
            if (Enum.TryParse(name, ignoreCase: true, out TlsCipherSuite suite))
            {
                suites.Add(suite);
            }
        }

        return suites.ToImmutable();
    }
}
