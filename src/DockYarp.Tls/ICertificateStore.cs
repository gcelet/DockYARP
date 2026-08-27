namespace DockYarp.Tls;

using System.Collections.Generic;

/// <summary>Stores certificates by host and serves them for SNI selection.</summary>
public interface ICertificateStore
{
    /// <summary>Finds the certificate for a host, if any.</summary>
    /// <param name="host">The host name.</param>
    /// <returns>The certificate, or <see langword="null"/> when none is stored.</returns>
    LoadedCertificate? Find(string host);

    /// <summary>Stores (or replaces) the certificate for a host, persisting it.</summary>
    /// <param name="host">The host name.</param>
    /// <param name="certificate">The certificate to store.</param>
    void Save(string host, LoadedCertificate certificate);

    /// <summary>Lists summary information for all stored certificates.</summary>
    /// <returns>The stored certificate summaries.</returns>
    IReadOnlyList<CertificateInfo> List();

    /// <summary>Checks whether a host's certificate is currently backed by a legacy <c>.pfx</c> file rather
    /// than the canonical <c>.crt</c>/<c>.key</c> PEM pair.</summary>
    /// <param name="host">The host to check.</param>
    /// <returns><see langword="true"/> when a <c>.pfx</c> file exists for <paramref name="host"/> with no
    /// corresponding PEM pair; <see langword="false"/> otherwise (including when the host has no certificate
    /// at all).</returns>
    bool IsPfxBacked(string host);

    /// <summary>Rewrites a <c>.pfx</c>-backed host's already-loaded certificate as a <c>.crt</c>/<c>.key</c>
    /// PEM pair and removes the stale <c>.pfx</c> file. Does not re-provision or change what is served.</summary>
    /// <param name="host">The host to convert.</param>
    /// <returns><see langword="true"/> when a certificate was found for <paramref name="host"/> and converted;
    /// <see langword="false"/> when no certificate is stored for it.</returns>
    bool ConvertToPem(string host);

    /// <summary>Rewrites a host's already-loaded certificate's private key under the currently configured
    /// encryption passphrase, whether that's a first-time enable (the key was plain) or a rotation (the key
    /// was encrypted with a previous passphrase). Does not touch <c>.crt</c> content or any <c>.pfx</c> file.</summary>
    /// <param name="host">The host whose key to re-encrypt.</param>
    /// <returns><see langword="true"/> when a certificate was found for <paramref name="host"/> and its key
    /// rewritten; <see langword="false"/> when no certificate is stored for it.</returns>
    bool ReencryptPrivateKey(string host);

    /// <summary>Checks whether a host's private key still needs re-encryption onto the currently configured
    /// passphrase.</summary>
    /// <param name="host">The host to check.</param>
    /// <returns><see langword="true"/> when a passphrase is configured but the host's key was loaded plain or
    /// via the previous passphrase fallback; <see langword="false"/> otherwise (including when no passphrase is
    /// configured, the key already matches the current passphrase, or the host has no PEM-loaded key at all —
    /// e.g. a <c>.pfx</c>-backed host, for which <see cref="ConvertToPem"/> already applies the current
    /// passphrase).</returns>
    bool RequiresKeyReencryption(string host);

    /// <summary>Removes a host's stored certificate — its <c>.crt</c>/<c>.key</c> PEM pair and any legacy
    /// <c>.pfx</c> file — and drops it from lookup. Used after a successful ACME revocation, so the
    /// provisioning/renewal reconcile loop requests a fresh certificate (with a fresh key) on its next pass.</summary>
    /// <param name="host">The host whose certificate to remove.</param>
    /// <returns><see langword="true"/> when a certificate was found for <paramref name="host"/> and removed;
    /// <see langword="false"/> when no certificate is stored for it.</returns>
    bool Remove(string host);
}
