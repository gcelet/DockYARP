namespace DockYarp.Tls;

using System;
using System.Collections.Frozen;
using System.Globalization;
using System.IO.Abstractions;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;

using Org.BouncyCastle.X509;

/// <summary>Validates client certificates against a configured CA and CRL (for mutual TLS).</summary>
/// <remarks>The CA and CRL are loaded once from PEM files; chain validation is offline (custom trust, no live
/// revocation fetch) — <see cref="TlsOptions.ClientCrlPath"/> is the sole opt-in revocation mechanism.</remarks>
public sealed class ClientCertificateValidator
{
    private readonly X509Certificate2Collection caCertificates = [];
    private readonly FrozenSet<BigInteger> revokedSerialNumbers;

    /// <summary>Loads the client CA certificate(s) and CRL from their configured paths, if any.</summary>
    /// <param name="options">TLS options carrying the client CA and CRL paths.</param>
    /// <param name="fileSystem">Filesystem abstraction used to read the CA/CRL files.</param>
    public ClientCertificateValidator(TlsOptions options, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fileSystem);

        if (options.ClientCaCertificatePath is { Length: > 0 } path && fileSystem.File.Exists(path))
        {
            caCertificates.ImportFromPem(fileSystem.File.ReadAllText(path));
        }

        revokedSerialNumbers = LoadRevokedSerialNumbers(options.ClientCrlPath, fileSystem);
    }

    /// <summary>Gets a value indicating whether a client CA is configured (mutual TLS is enabled).</summary>
    public bool HasClientCa => caCertificates.Count > 0;

    /// <summary>Validates that a client certificate chains to the configured CA and is not revoked.</summary>
    /// <param name="certificate">The presented client certificate.</param>
    /// <returns><see langword="true"/> when the certificate chains to the CA and is not listed in the
    /// configured CRL; otherwise <see langword="false"/>.</returns>
    public bool Validate(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        if (!HasClientCa)
        {
            return false;
        }

        using X509Chain chain = new();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.CustomTrustStore.AddRange(caCertificates);
        return chain.Build(certificate) && !revokedSerialNumbers.Contains(ParseSerialHex(certificate.SerialNumber));
    }

    private static FrozenSet<BigInteger> LoadRevokedSerialNumbers(string? path, IFileSystem fileSystem)
    {
        if (path is not { Length: > 0 } || !fileSystem.File.Exists(path))
        {
            return FrozenSet<BigInteger>.Empty;
        }

        using System.IO.Stream stream = fileSystem.File.OpenRead(path);
        X509Crl crl = new X509CrlParser().ReadCrl(stream);
        return crl.GetRevokedCertificates()
            .Cast<X509CrlEntry>()
            .Select(entry => ParseSerialHex(entry.SerialNumber.ToString(16)))
            .ToFrozenSet();
    }

    // .NET's X509Certificate2.SerialNumber and BouncyCastle's BigInteger.ToString(16) both render an unsigned
    // hex string; a leading "0" forces System.Numerics.BigInteger.Parse to treat it as positive (HexNumber
    // parsing otherwise reads a high bit as a two's-complement sign), so the two representations compare equal.
    private static BigInteger ParseSerialHex(string hex) =>
        BigInteger.Parse("0" + hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
