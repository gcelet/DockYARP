namespace DockYarp.Security;

/// <summary>Options controlling at-rest encryption of the Data Protection key ring.</summary>
public sealed record DataProtectionOptions
{
    /// <summary>Gets the path to a PKCS#12 (PFX) certificate used to encrypt the persisted key ring at rest.</summary>
    /// <remarks>
    /// When null or empty, the key ring is persisted unencrypted and the benign "keys may be persisted
    /// unencrypted" warning is suppressed, because no DockYarp feature currently protects a sensitive payload with
    /// Data Protection. For real at-rest protection, store this certificate <b>outside</b> the state
    /// (<c>/certs</c>) volume so its private key does not sit next to the keys it protects.
    /// </remarks>
    public string? CertificatePath { get; init; }

    /// <summary>Gets the password for the PKCS#12 certificate named by <see cref="CertificatePath"/>, if any.</summary>
    public string? CertificatePassword { get; init; }
}
