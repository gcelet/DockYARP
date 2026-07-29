namespace DockYarp.Security;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

/// <summary>Verifies a password against an htpasswd hash: bcrypt, Apache apr1, or SHA1.</summary>
internal static class HtpasswdVerifier
{
    /// <summary>Verifies a password against a stored htpasswd hash.</summary>
    /// <param name="password">The presented password.</param>
    /// <param name="hash">The stored hash (bcrypt, apr1, or <c>{SHA}</c>).</param>
    /// <returns><see langword="true"/> when the password matches; <see langword="false"/> for a mismatch or an
    /// unrecognized hash format.</returns>
    public static bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(hash) || password is null)
        {
            return false;
        }

        if (hash.StartsWith("$2a$", StringComparison.Ordinal)
            || hash.StartsWith("$2b$", StringComparison.Ordinal)
            || hash.StartsWith("$2y$", StringComparison.Ordinal))
        {
            return VerifyBCrypt(password, hash);
        }

        if (hash.StartsWith("$apr1$", StringComparison.Ordinal))
        {
            return Apr1.Verify(password, hash);
        }

        if (hash.StartsWith("{SHA}", StringComparison.Ordinal))
        {
            return VerifySha1(password, hash["{SHA}".Length..]);
        }

        return false;
    }

    private static bool VerifyBCrypt(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

    // SHA1 is weak but required for htpasswd's {SHA} format; this verifies an existing digest, it does not mint one.
    [SuppressMessage(
        "Security",
        "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "htpasswd {SHA} entries are SHA1 by definition; verification must use it for compatibility.")]
    [SuppressMessage(
        "Minor Code Smell",
        "S4790:Using weak hashing algorithms is security-sensitive",
        Justification = "htpasswd {SHA} entries are SHA1 by definition; verification must use it for compatibility.")]
    private static bool VerifySha1(string password, string expectedBase64)
    {
        byte[] digest = SHA1.HashData(Encoding.UTF8.GetBytes(password));
        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(expectedBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(digest, expected);
    }
}
