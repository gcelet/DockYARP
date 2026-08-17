namespace DockYarp.Tls;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

/// <summary>Loads a PKCS12 blob as a <see cref="LoadedCertificate"/>, splitting the keyed leaf from any
/// additional (chain) certificates in the same bag.</summary>
internal static class CertificateCollectionLoader
{
    /// <summary>Loads every certificate in <paramref name="pkcs12"/> and splits the one carrying the private
    /// key from the rest.</summary>
    /// <param name="pkcs12">The PKCS12 (PFX) bytes.</param>
    /// <param name="password">The password the PKCS12 bag was protected with, or <see langword="null"/>.</param>
    /// <returns>The keyed leaf plus any additional certificates found alongside it.</returns>
    /// <exception cref="InvalidOperationException">No certificate in the bag has a private key.</exception>
    public static LoadedCertificate LoadKeyed(byte[] pkcs12, string? password)
    {
        X509Certificate2Collection loaded = X509CertificateLoader.LoadPkcs12Collection(pkcs12, password);
        X509Certificate2? leaf = loaded.FirstOrDefault(entry => entry.HasPrivateKey);
        if (leaf is null)
        {
            throw new InvalidOperationException("The PKCS12 data does not contain a certificate with a private key.");
        }

        List<X509Certificate2> additional = [.. loaded.Where(entry => !ReferenceEquals(entry, leaf))];
        return new LoadedCertificate(leaf, additional);
    }
}
