namespace DockYarp.Tls;

/// <summary>The current and (optional) previous private-key encryption passphrase, used as a load-side
/// decryption fallback while a passphrase rotation is in progress.</summary>
/// <param name="Current">The current <c>Tls:PrivateKeyEncryptionPassphrase</c>, or null/empty when unset.</param>
/// <param name="Previous">The current <c>Tls:PreviousPrivateKeyEncryptionPassphrase</c>, or null/empty when unset.</param>
internal readonly record struct PrivateKeyPassphrases(string? Current, string? Previous)
{
    /// <summary>No passphrases configured — matches today's plain-PEM-only behavior.</summary>
    public static readonly PrivateKeyPassphrases None = new(null, null);
}
