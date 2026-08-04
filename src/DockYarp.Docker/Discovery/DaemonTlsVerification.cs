namespace DockYarp.Docker.Discovery;

/// <summary>How the Docker daemon's TLS certificate is verified when connecting over <c>tcp://</c>.</summary>
public enum DaemonTlsVerification
{
    /// <summary>Do not verify the daemon certificate (the connection is encrypted but the peer is unchecked).</summary>
    AcceptAny,

    /// <summary>Verify the daemon certificate against the configured CA (<c>ca.pem</c>) via custom root trust.</summary>
    VerifyAgainstCa,
}
