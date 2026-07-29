namespace DockYarp.Security;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

/// <summary>Verifies passwords against Apache's apr1 (MD5-crypt) hashes.</summary>
/// <remarks>
/// apr1 mandates MD5 by specification; this is verify-only, for htpasswd compatibility, not a security choice.
/// Implemented from the documented algorithm (Apache <c>apr_md5.c</c>) and pinned by a known-answer test.
/// </remarks>
[SuppressMessage(
    "Security",
    "CA5351:Do Not Use Broken Cryptographic Algorithms",
    Justification = "apr1 htpasswd hashes are defined in terms of MD5; verification must use it for compatibility.")]
[SuppressMessage(
    "Minor Code Smell",
    "S4790:Using weak hashing algorithms is security-sensitive",
    Justification = "apr1 htpasswd hashes are defined in terms of MD5; verification must use it for compatibility.")]
internal static class Apr1
{
    private const string Magic = "$apr1$";
    private const string Base64Alphabet = "./0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>Verifies a password against an apr1 hash of the form <c>$apr1$salt$checksum</c>.</summary>
    /// <param name="password">The password to check.</param>
    /// <param name="hash">The stored apr1 hash.</param>
    /// <returns><see langword="true"/> when the password matches the hash.</returns>
    public static bool Verify(string password, string hash)
    {
        if (password is null || hash is null || !hash.StartsWith(Magic, StringComparison.Ordinal))
        {
            return false;
        }

        // Salt is up to 8 characters between the second and third '$'.
        string rest = hash[Magic.Length..];
        int end = rest.IndexOf('$', StringComparison.Ordinal);
        string salt = end >= 0 ? rest[..end] : rest;
        if (salt.Length > 8)
        {
            salt = salt[..8];
        }

        string computed = Hash(password, salt);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computed), Encoding.ASCII.GetBytes(hash));
    }

    /// <summary>Computes the apr1 hash string for a password and salt.</summary>
    /// <param name="password">The password.</param>
    /// <param name="salt">The salt (up to 8 characters).</param>
    /// <returns>The <c>$apr1$salt$checksum</c> hash string.</returns>
    public static string Hash(string password, string salt)
    {
        byte[] pw = Encoding.UTF8.GetBytes(password);
        byte[] saltBytes = Encoding.ASCII.GetBytes(salt);

        // Alternate digest: password + salt + password.
        byte[] altInput = [.. pw, .. saltBytes, .. pw];
        byte[] alt = MD5.HashData(altInput);

        // Primary context: password + magic + salt, one alt-digest block per 16 password bytes, then a mix
        // that leaks the password length one bit at a time.
        List<byte> ctx = [.. pw, .. Encoding.ASCII.GetBytes(Magic), .. saltBytes];
        for (int i = pw.Length; i > 0; i -= 16)
        {
            ctx.AddRange(alt.AsSpan(0, Math.Min(i, 16)));
        }

        for (int i = pw.Length; i > 0; i >>= 1)
        {
            ctx.Add((i & 1) != 0 ? (byte)0 : pw[0]);
        }

        byte[] final = MD5.HashData(ctx.ToArray());

        // 1000 iterations, alternating the order of password/salt/previous-digest.
        for (int i = 0; i < 1000; i++)
        {
            List<byte> round = [];
            round.AddRange((i & 1) != 0 ? pw : final);
            if (i % 3 != 0)
            {
                round.AddRange(saltBytes);
            }

            if (i % 7 != 0)
            {
                round.AddRange(pw);
            }

            round.AddRange((i & 1) != 0 ? final : pw);
            final = MD5.HashData(round.ToArray());
        }

        return $"{Magic}{salt}${Encode(final)}";
    }

    private static string Encode(byte[] f)
    {
        StringBuilder builder = new(22);
        To64(builder, (f[0] << 16) | (f[6] << 8) | f[12], 4);
        To64(builder, (f[1] << 16) | (f[7] << 8) | f[13], 4);
        To64(builder, (f[2] << 16) | (f[8] << 8) | f[14], 4);
        To64(builder, (f[3] << 16) | (f[9] << 8) | f[15], 4);
        To64(builder, (f[4] << 16) | (f[10] << 8) | f[5], 4);
        To64(builder, f[11], 2);
        return builder.ToString();
    }

    private static void To64(StringBuilder builder, int value, int count)
    {
        uint v = (uint)value;
        for (int i = 0; i < count; i++)
        {
            builder.Append(Base64Alphabet[(int)(v & 0x3f)]);
            v >>= 6;
        }
    }
}
