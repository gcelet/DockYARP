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
}
