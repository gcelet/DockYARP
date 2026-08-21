namespace DockYarp.AdminApi;

/// <summary>Lets the admin dashboard rewrite a legacy PFX-backed certificate as the canonical PEM pair.</summary>
/// <remarks>Implemented by the host over the TLS certificate store, keeping AdminApi decoupled from TLS. This
/// is the one mutating capability the admin surface exposes — see "Read-only admin dashboard" for the narrow,
/// explicit carve-out this represents.</remarks>
public interface ICertificateConverter
{
    /// <summary>Checks whether a host's certificate is currently backed by a legacy PFX file.</summary>
    /// <param name="host">The host to check.</param>
    /// <returns><see langword="true"/> when the host has a PFX-backed certificate with no PEM pair yet.</returns>
    bool IsPfxBacked(string host);

    /// <summary>Gets a value indicating whether at-rest private-key encryption is currently configured.</summary>
    /// <remarks>Covers both a first-time enable and a passphrase rotation with the same single condition — see
    /// <c>add-tls-private-key-encryption</c>'s design for why gating on a "previous" passphrase alone would miss
    /// the first-time-enable case.</remarks>
    bool PrivateKeyEncryptionConfigured { get; }

    /// <summary>Checks whether a host's private key still needs re-encryption onto the currently configured
    /// passphrase (loaded plain, or via the previous-passphrase fallback).</summary>
    /// <param name="host">The host to check.</param>
    /// <returns><see langword="true"/> when the host's key has not yet been rewritten under the current
    /// passphrase.</returns>
    bool RequiresKeyReencryption(string host);

    /// <summary>Rewrites a PFX-backed host's certificate as a PEM pair, removing the stale PFX file.</summary>
    /// <param name="host">The host to convert.</param>
    /// <returns><see langword="true"/> when a certificate was found for <paramref name="host"/> and converted;
    /// <see langword="false"/> when no certificate is stored for it.</returns>
    bool ConvertToPem(string host);

    /// <summary>Rewrites a host's stored private key under the currently configured encryption passphrase.</summary>
    /// <param name="host">The host to re-encrypt.</param>
    /// <returns><see langword="true"/> when a certificate was found for <paramref name="host"/> and its key
    /// rewritten; <see langword="false"/> when no certificate is stored for it.</returns>
    bool ReencryptPrivateKey(string host);
}
