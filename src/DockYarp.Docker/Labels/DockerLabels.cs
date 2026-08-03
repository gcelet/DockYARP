namespace DockYarp.Docker.Labels;

/// <summary>Well-known Docker label keys understood by DockYarp.</summary>
/// <remarks>The <c>VIRTUAL_*</c> and <c>LETSENCRYPT_*</c> keys are nginx-proxy compatible.</remarks>
public static class DockerLabels
{
    /// <summary>Host the container should be exposed on.</summary>
    public const string VirtualHost = "VIRTUAL_HOST";

    /// <summary>YAML mapping of host → path → <c>{ port, proto, dest }</c> for multi-port containers.</summary>
    public const string VirtualHostMultiports = "VIRTUAL_HOST_MULTIPORTS";

    /// <summary>Target container port.</summary>
    public const string VirtualPort = "VIRTUAL_PORT";

    /// <summary>Optional path prefix the route matches.</summary>
    public const string VirtualPath = "VIRTUAL_PATH";

    /// <summary>Backend transport scheme (<c>http</c> or <c>https</c>); defaults to <c>http</c>.</summary>
    public const string VirtualProto = "VIRTUAL_PROTO";

    /// <summary>Destination path rewrite; <c>/</c> strips the <see cref="VirtualPath"/> prefix before forwarding.</summary>
    public const string VirtualDest = "VIRTUAL_DEST";

    /// <summary>Host a certificate should be obtained for.</summary>
    public const string LetsEncryptHost = "LETSENCRYPT_HOST";

    /// <summary>Contact email used when requesting the certificate.</summary>
    public const string LetsEncryptEmail = "LETSENCRYPT_EMAIL";

    /// <summary>Name of a shared certificate in the store to pin for this host (overrides per-host selection).</summary>
    public const string CertName = "CERT_NAME";

    /// <summary>Named TLS preset for this host (<c>Mozilla-Modern</c>/<c>Intermediate</c>/<c>Old</c>); overrides the global policy.</summary>
    public const string SslPolicy = "SSL_POLICY";

    /// <summary>Per-host <c>Server</c> header control (<c>off</c> suppresses it for the host).</summary>
    public const string ServerTokens = "SERVER_TOKENS";

    /// <summary>External HTTPS port used in the HTTP→HTTPS redirect target for this host (default 443).</summary>
    public const string ExternalHttpsPort = "EXTERNAL_HTTPS_PORT";

    /// <summary>Per-host override of whether to serve HTTP when the host has no certificate (default from global).</summary>
    public const string EnableHttpOnMissingCert = "ENABLE_HTTP_ON_MISSING_CERT";

    /// <summary>Per-host override of whether the host may fall back to the default certificate (default from global).</summary>
    public const string TrustDefaultCert = "TRUST_DEFAULT_CERT";

    /// <summary>nginx-proxy namespaced label for per-vhost trust-default-cert; aliases <see cref="TrustDefaultCert"/>.</summary>
    public const string NginxTrustDefaultCert = "com.github.nginx-proxy.nginx-proxy.trust-default-cert";

    /// <summary>HTTP↔HTTPS behavior: <c>redirect</c> (default), <c>noredirect</c>, <c>nohttp</c>, <c>nohttps</c>.</summary>
    public const string HttpsMethod = "HTTPS_METHOD";

    /// <summary>Per-host HSTS policy: a <c>Strict-Transport-Security</c> value, or <c>off</c> to disable it.</summary>
    public const string Hsts = "HSTS";

    /// <summary>DockYarp-specific load-balancing policy (<c>round-robin</c> or <c>least-requests</c>).</summary>
    public const string LoadBalancing = "DOCKYARP_LB";

    /// <summary>Route priority; higher wins when several routes could match (default <c>0</c>).</summary>
    public const string Priority = "DOCKYARP_PRIORITY";

    /// <summary>Client-certificate (mutual TLS) requirement: <c>required</c>, <c>optional</c>, or <c>none</c>/<c>off</c>.</summary>
    public const string ClientCert = "DOCKYARP_CLIENT_CERT";

    /// <summary>Proxy request timeout in seconds applied to the cluster's outgoing requests.</summary>
    public const string ProxyTimeout = "DOCKYARP_PROXY_TIMEOUT";

    /// <summary>Maximum request body size in bytes accepted for the route.</summary>
    public const string MaxBodySize = "DOCKYARP_MAX_BODY_SIZE";

    /// <summary>Basic Auth username protecting the route.</summary>
    public const string AuthUser = "DOCKYARP_AUTH_USER";

    /// <summary>Basic Auth password protecting the route.</summary>
    public const string AuthPassword = "DOCKYARP_AUTH_PASSWORD";

    /// <summary>Optional Basic Auth realm shown in the challenge.</summary>
    public const string AuthRealm = "DOCKYARP_AUTH_REALM";

    /// <summary>Restricts the route to internal client networks when set to <c>internal</c> (returns 403 otherwise).</summary>
    public const string NetworkAccess = "NETWORK_ACCESS";

    /// <summary>nginx-proxy namespaced label for the load-balancing policy; aliases <see cref="LoadBalancing"/>.</summary>
    public const string NginxLoadBalance = "com.github.nginx-proxy.nginx-proxy.loadbalance";

    /// <summary>nginx-proxy namespaced label for mutual-TLS verification; aliases <see cref="ClientCert"/>.</summary>
    public const string NginxSslVerifyClient = "com.github.nginx-proxy.nginx-proxy.ssl_verify_client";
}
