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
}
