namespace DockYarp.Docker.Labels;

/// <summary>Well-known Docker label keys understood by DockYarp.</summary>
/// <remarks>The <c>VIRTUAL_*</c> and <c>LETSENCRYPT_*</c> keys are nginx-proxy compatible.</remarks>
public static class DockerLabels
{
    /// <summary>Host the container should be exposed on.</summary>
    public const string VirtualHost = "VIRTUAL_HOST";

    /// <summary>Target container port.</summary>
    public const string VirtualPort = "VIRTUAL_PORT";

    /// <summary>Optional path prefix the route matches.</summary>
    public const string VirtualPath = "VIRTUAL_PATH";

    /// <summary>Backend transport scheme (<c>http</c> or <c>https</c>); defaults to <c>http</c>.</summary>
    public const string VirtualProto = "VIRTUAL_PROTO";

    /// <summary>Host a certificate should be obtained for.</summary>
    public const string LetsEncryptHost = "LETSENCRYPT_HOST";

    /// <summary>Contact email used when requesting the certificate.</summary>
    public const string LetsEncryptEmail = "LETSENCRYPT_EMAIL";

    /// <summary>DockYarp-specific load-balancing policy (<c>round-robin</c> or <c>least-requests</c>).</summary>
    public const string LoadBalancing = "DOCKYARP_LB";

    /// <summary>Basic Auth username protecting the route.</summary>
    public const string AuthUser = "DOCKYARP_AUTH_USER";

    /// <summary>Basic Auth password protecting the route.</summary>
    public const string AuthPassword = "DOCKYARP_AUTH_PASSWORD";

    /// <summary>Optional Basic Auth realm shown in the challenge.</summary>
    public const string AuthRealm = "DOCKYARP_AUTH_REALM";
}
