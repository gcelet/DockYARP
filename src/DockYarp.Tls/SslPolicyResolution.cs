namespace DockYarp.Tls;

using System.Collections.Immutable;

/// <summary>The effective TLS posture: minimum version and cipher-suite names.</summary>
/// <param name="MinimumTlsVersion">The minimum accepted TLS version.</param>
/// <param name="CipherSuites">The cipher-suite names (empty when none are configured).</param>
public readonly record struct SslPolicyResolution(TlsVersion MinimumTlsVersion, ImmutableArray<string> CipherSuites);
