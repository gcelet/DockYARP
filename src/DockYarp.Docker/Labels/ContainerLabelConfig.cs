namespace DockYarp.Docker.Labels;

using System;
using System.Collections.Immutable;

using DockYarp.Core.Models;

/// <summary>Strongly-typed configuration parsed from a container's labels.</summary>
public sealed record ContainerLabelConfig
{
    /// <summary>Gets the hosts the container is exposed on (comma-separated <c>VIRTUAL_HOST</c>).</summary>
    public required ImmutableArray<string> Hosts { get; init; }

    /// <summary>Gets the target container port (<c>VIRTUAL_PORT</c>, or inferred).</summary>
    public required int Port { get; init; }

    /// <summary>Gets the backend transport scheme (<c>VIRTUAL_PROTO</c>); defaults to HTTP.</summary>
    public BackendScheme Scheme { get; init; } = BackendScheme.Http;

    /// <summary>Gets a value indicating whether the backend uses HTTP/2 only (<c>VIRTUAL_PROTO=grpc</c>/<c>grpcs</c>).</summary>
    public bool Http2 { get; init; }

    /// <summary>Gets the optional path prefix (<c>VIRTUAL_PATH</c>).</summary>
    public string? PathPrefix { get; init; }

    /// <summary>Gets the optional prefix stripped before forwarding (derived from <c>VIRTUAL_DEST</c>).</summary>
    public string? PathRemovePrefix { get; init; }

    /// <summary>Gets the optional destination prefix prepended after stripping (derived from <c>VIRTUAL_DEST</c>).</summary>
    public string? PathAddPrefix { get; init; }

    /// <summary>Gets the certificate host (<c>LETSENCRYPT_HOST</c>), if any.</summary>
    public string? LetsEncryptHost { get; init; }

    /// <summary>Gets the certificate contact email (<c>LETSENCRYPT_EMAIL</c>), if any.</summary>
    public string? LetsEncryptEmail { get; init; }

    /// <summary>Gets the shared-certificate name (<c>CERT_NAME</c>) pinned for the host, if any.</summary>
    public string? CertName { get; init; }

    /// <summary>Gets the per-host TLS preset (<c>SSL_POLICY</c>) overriding the global policy, if any.</summary>
    public string? SslPolicy { get; init; }

    /// <summary>Gets the per-host frontend HTTP/2 toggle (<c>DOCKYARP_HTTP2</c>); <see langword="null"/> uses the global protocols.</summary>
    /// <remarks>Controls whether HTTP/2 is offered to clients via ALPN for this host; distinct from <see cref="Http2"/> (the backend transport).</remarks>
    public bool? Http2Enabled { get; init; }

    /// <summary>Gets the per-host <c>Server</c> header control (<c>SERVER_TOKENS</c>); <c>off</c> suppresses it.</summary>
    public string? ServerTokens { get; init; }

    /// <summary>Gets the external HTTPS port (<c>EXTERNAL_HTTPS_PORT</c>) used in the redirect target, if any.</summary>
    public int? ExternalHttpsPort { get; init; }

    /// <summary>Gets the per-host <c>ENABLE_HTTP_ON_MISSING_CERT</c> override; <see langword="null"/> uses the global default.</summary>
    public bool? EnableHttpOnMissingCert { get; init; }

    /// <summary>Gets the per-host <c>TRUST_DEFAULT_CERT</c> override; <see langword="null"/> uses the global default.</summary>
    public bool? TrustDefaultCert { get; init; }

    /// <summary>Gets the HTTPS method (<c>HTTPS_METHOD</c>); defaults to <see cref="Core.Models.HttpsMethod.Redirect"/>.</summary>
    public HttpsMethod HttpsMethod { get; init; } = HttpsMethod.Redirect;

    /// <summary>Gets the per-host HSTS override (<c>HSTS</c>), if any.</summary>
    public string? Hsts { get; init; }

    /// <summary>Gets the requested load-balancing policy (<c>DOCKYARP_LB</c>), if any.</summary>
    public LoadBalancingPolicy? LoadBalancingPolicy { get; init; }

    /// <summary>Gets the requested session affinity policy (<c>DOCKYARP_AFFINITY</c>), if any.</summary>
    public SessionAffinityPolicy? SessionAffinityPolicy { get; init; }

    /// <summary>Gets the route priority (<c>DOCKYARP_PRIORITY</c>); higher wins, default <c>0</c>.</summary>
    public int Priority { get; init; }

    /// <summary>Gets the client-certificate requirement (<c>DOCKYARP_CLIENT_CERT</c>); defaults to none.</summary>
    public ClientCertificateRequirement ClientCertificate { get; init; } = ClientCertificateRequirement.None;

    /// <summary>Gets the optional proxy request timeout (<c>DOCKYARP_PROXY_TIMEOUT</c>).</summary>
    public TimeSpan? ProxyTimeout { get; init; }

    /// <summary>Gets the optional maximum request body size in bytes (<c>DOCKYARP_MAX_BODY_SIZE</c>).</summary>
    public long? MaxRequestBodySize { get; init; }

    /// <summary>Gets the maximum concurrent connections opened to the cluster's backend, if set.</summary>
    public int? MaxConnectionsPerServer { get; init; }

    /// <summary>Gets the Basic Auth credentials (<c>DOCKYARP_AUTH_*</c>), set only when complete.</summary>
    public BasicAuthCredentials? Auth { get; init; }

    /// <summary>Gets a value indicating whether the route is restricted to internal networks (<c>NETWORK_ACCESS=internal</c>).</summary>
    public bool InternalOnly { get; init; }
}
