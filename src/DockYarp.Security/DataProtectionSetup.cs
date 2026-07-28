namespace DockYarp.Security;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Configures DockYarp's Data Protection key ring: persistence plus optional at-rest encryption.</summary>
public static class DataProtectionSetup
{
    // The single log category that emits the benign "keys may be persisted unencrypted" warning (event 35).
    // Its floor is raised only when no encryption certificate is configured; see AddDockYarpDataProtection.
    private const string XmlKeyManagerCategory = "Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager";

    /// <summary>Persists the key ring and, when an encryption certificate is configured, protects it at rest.</summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="options">Data Protection options (the encryption certificate, if any).</param>
    /// <param name="keyDirectory">The directory the key ring is persisted under (a <c>dataprotection-keys</c> subfolder).</param>
    /// <returns>Whether the key ring is encrypted at rest, or its unencrypted-keys warning was suppressed.</returns>
    /// <remarks>
    /// Data Protection is registered transitively (YARP uses it for session affinity) and ASP.NET initializes the
    /// key ring at startup even though DockYarp currently protects no sensitive payload (no affinity, cookies, or
    /// auth). With no encryption certificate the key ring is persisted unencrypted and the resulting
    /// <c>XmlKeyManager</c> warning is benign, so it is suppressed; supplying a certificate enables real at-rest
    /// encryption (and the warning disappears on its own). A future Data-Protection-consuming feature must instead
    /// <b>require</b> the certificate and fail fast — tracked on the <c>add-loadbalance-policies</c> backlog item.
    /// </remarks>
    public static DataProtectionKeyEncryption AddDockYarpDataProtection(
        this IHostApplicationBuilder builder, DataProtectionOptions options, string keyDirectory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(keyDirectory);

        IDataProtectionBuilder dataProtection = builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(keyDirectory, "dataprotection-keys")))
            .SetApplicationName("dockyarp");

        X509Certificate2? certificate = LoadEncryptionCertificate(options);
        if (certificate is not null)
        {
            // Ownership is transferred to Data Protection, which keeps the certificate for the process lifetime.
            dataProtection.ProtectKeysWithCertificate(certificate);
            return DataProtectionKeyEncryption.Encrypted;
        }

        // No sensitive payload is protected, so the "unencrypted keys" warning is noise: raise only that
        // category's floor. Once a certificate is configured this branch is skipped and the warning is gone.
        builder.Logging.AddFilter(XmlKeyManagerCategory, LogLevel.Error);
        return DataProtectionKeyEncryption.SuppressedUnencrypted;
    }

    /// <summary>Loads the configured Data Protection encryption certificate, or null when none is configured.</summary>
    /// <param name="options">Data Protection options.</param>
    /// <returns>The certificate (with its private key), or null when no path is configured.</returns>
    /// <exception cref="InvalidOperationException">The configured certificate cannot be found or loaded.</exception>
    public static X509Certificate2? LoadEncryptionCertificate(DataProtectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.CertificatePath))
        {
            return null;
        }

        string path = options.CertificatePath;
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"DataProtection:CertificatePath '{path}' was not found. Provide a readable PKCS#12 (PFX) file, or unset it to persist keys unencrypted.");
        }

        try
        {
            return X509CertificateLoader.LoadPkcs12FromFile(path, options.CertificatePassword);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                $"Failed to load the DataProtection encryption certificate '{path}'. Ensure it is a PKCS#12 (PFX) file with a private key and that DataProtection:CertificatePassword is correct.", ex);
        }
    }
}
