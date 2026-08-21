namespace DockYarp.Tls;

/// <summary>The outcome of loading a PEM certificate/key pair.</summary>
internal sealed record PemLoadResult
{
    /// <summary>Gets the loaded certificate, with its full chain preserved.</summary>
    public required LoadedCertificate Certificate { get; init; }

    /// <summary>
    /// Gets a value indicating whether a current passphrase is configured but the key on disk was not
    /// encrypted with it (plain, or still under the previous passphrase) — signals that the dashboard's
    /// re-encryption action has not yet been applied to this host.
    /// </summary>
    public required bool RequiresReencryption { get; init; }
}
