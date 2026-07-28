namespace DockYarp.Security;

/// <summary>The at-rest disposition of DockYarp's Data Protection key ring.</summary>
public enum DataProtectionKeyEncryption
{
    /// <summary>The key ring is encrypted at rest with the operator-supplied certificate.</summary>
    Encrypted,

    /// <summary>No certificate is configured; keys are persisted unencrypted and the benign warning is suppressed.</summary>
    SuppressedUnencrypted,
}
